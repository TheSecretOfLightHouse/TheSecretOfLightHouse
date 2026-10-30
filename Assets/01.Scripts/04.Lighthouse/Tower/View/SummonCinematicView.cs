using System;
using System.Collections;
using System.Collections.Generic;
using Lighthouse.Common.Cinematics;
using Lighthouse.Tower.Net;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Lighthouse.Tower.View
{
    [DefaultExecutionOrder(10000)]
    public sealed class SummonCinematicView : MonoBehaviour
    {
        private readonly Dictionary<Behaviour, bool> _saved = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<EventSystem, GameObject> _selected = new Dictionary<EventSystem, GameObject>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<Behaviour> _behaviours = new List<Behaviour>();
        private TowerMatchNet _match;
        private Coroutine _routine;
        private AsyncOperation _loading;
        private Scene _presentation;
        private string _loadingPath;
        private uint _sequence;
        private bool _active;
        private bool _showing;
        private bool _loadCleanupRegistered;
        private float _nextScanAt;
        private Camera _camera;
        private AudioListener _listener;
        private PlayableDirector _director;

        public void Initialize(TowerMatchNet match)
        {
            _match = match;
        }

        private void Update()
        {
            if (_match == null || !NetworkClient.active || !NetworkClient.ready || !_match.IsSummonCinematicPlaying)
            {
                Cleanup();
                return;
            }
            if (!_active || _sequence != _match.CinematicSequence)
            {
                Cleanup();
                _active = true;
                _sequence = _match.CinematicSequence;
                CinematicPresentationLock.Acquire(this);
                _routine = StartCoroutine(Present());
            }
        }

        private void LateUpdate()
        {
            if (!_active)
            {
                return;
            }
            if (Time.unscaledTime >= _nextScanAt)
            {
                _nextScanAt = Time.unscaledTime + 0.25f;
                CaptureScenes();
            }
            foreach (KeyValuePair<Behaviour, bool> entry in _saved)
            {
                if (entry.Key != null && (_showing || entry.Key is EventSystem))
                {
                    entry.Key.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private bool IsCurrent => _active && _match != null && NetworkClient.active && NetworkClient.ready
            && _match.IsSummonCinematicPlaying && _sequence == _match.CinematicSequence;

        private IEnumerator Present()
        {
            _loadingPath = _match.CinematicScenePath;
            bool skipped = string.IsNullOrEmpty(_loadingPath) || !Application.CanStreamedLevelBeLoaded(_loadingPath);
            if (!skipped)
            {
                try
                {
                    _loading = SceneManager.LoadSceneAsync(_loadingPath, LoadSceneMode.Additive);
                }
                catch (Exception)
                {
                    skipped = true;
                }
            }
            while (!skipped && _loading != null && !_loading.isDone && IsCurrent)
            {
                if (NetworkTime.time >= _match.CinematicLoadDeadline || _match.CinematicPhase != SummonCinematicPhase.Preparing)
                {
                    skipped = true;
                    RegisterLateLoadCleanup();
                    break;
                }
                yield return null;
            }
            if (!IsCurrent)
            {
                yield break;
            }
            if (!skipped)
            {
                _presentation = SceneManager.GetSceneByPath(_loadingPath);
                skipped = !PreparePresentation();
            }
            if (skipped)
            {
                UnloadPresentation();
            }
            _match.CmdReportCinematicPrepared(_sequence);
            while (IsCurrent && _match.CinematicPhase == SummonCinematicPhase.Preparing)
            {
                yield return null;
            }
            while (IsCurrent && _match.CinematicPhase == SummonCinematicPhase.Playing)
            {
                double elapsed = NetworkTime.time - _match.SummonCinematicStartedAt;
                if (!skipped && elapsed >= 0d && NetworkTime.time < _match.SummonCinematicEndsAt)
                {
                    if (!TryEvaluate(elapsed))
                    {
                        skipped = true;
                        RestoreVisuals();
                        UnloadPresentation();
                    }
                }
                yield return null;
            }
            RestoreVisuals();
            AsyncOperation unloading = UnloadPresentation();
            while (unloading != null && !unloading.isDone && IsCurrent)
            {
                yield return null;
            }
            if (IsCurrent)
            {
                _match.CmdReportCinematicReturned(_sequence);
            }
            // Input remains locked until the server's shared Combat transition, including failed loads.
        }

        private bool PreparePresentation()
        {
            if (!_presentation.IsValid() || !_presentation.isLoaded)
            {
                return false;
            }
            _presentation.GetRootGameObjects(_roots);
            foreach (GameObject root in _roots)
            {
                root.GetComponentsInChildren(true, _behaviours);
                foreach (Behaviour behaviour in _behaviours)
                {
                    var camera = behaviour as Camera;
                    var listener = behaviour as AudioListener;
                    var director = behaviour as PlayableDirector;
                    if (camera != null)
                    {
                        camera.enabled = false;
                        if (_camera == null)
                        {
                            _camera = camera;
                        }
                    }
                    if (listener != null)
                    {
                        listener.enabled = false;
                        _listener = listener;
                    }
                    if (director != null)
                    {
                        director.playOnAwake = false;
                        director.Stop();
                        _director = director;
                    }
                }
            }
            return _camera != null && _director != null && _director.playableAsset != null
                && _director.duration > 0d && !double.IsInfinity(_director.duration) && !double.IsNaN(_director.duration);
        }

        private bool TryEvaluate(double elapsed)
        {
            try
            {
                if (!_showing)
                {
                    CaptureScenes();
                    _showing = true;
                    _camera.enabled = true;
                    if (_listener != null)
                    {
                        _listener.enabled = true;
                    }
                    _director.timeUpdateMode = DirectorUpdateMode.Manual;
                    _director.Play();
                }
                _director.time = Math.Min(elapsed, _director.duration);
                _director.Evaluate();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void CaptureScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                CaptureScene(SceneManager.GetSceneAt(i));
            }
            if (NetworkManager.singleton != null)
            {
                CaptureScene(NetworkManager.singleton.gameObject.scene);
            }
        }

        private void CaptureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene == _presentation)
            {
                return;
            }
            scene.GetRootGameObjects(_roots);
            foreach (GameObject root in _roots)
            {
                root.GetComponentsInChildren(true, _behaviours);
                foreach (Behaviour behaviour in _behaviours)
                {
                    if ((behaviour is Camera || behaviour is AudioListener || behaviour is Canvas || behaviour is EventSystem)
                        && !_saved.ContainsKey(behaviour))
                    {
                        _saved.Add(behaviour, behaviour.enabled);
                        var events = behaviour as EventSystem;
                        if (events != null)
                        {
                            _selected.Add(events, events.currentSelectedGameObject);
                        }
                    }
                }
            }
        }

        private void RestoreVisuals()
        {
            _showing = false;
            if (_director != null)
            {
                _director.Stop();
            }
            if (_camera != null)
            {
                _camera.enabled = false;
            }
            if (_listener != null)
            {
                _listener.enabled = false;
            }
            foreach (KeyValuePair<Behaviour, bool> entry in _saved)
            {
                if (entry.Key != null && !(entry.Key is EventSystem))
                {
                    entry.Key.enabled = entry.Value;
                }
            }
        }

        private AsyncOperation UnloadPresentation()
        {
            if (_presentation.IsValid() && _presentation.isLoaded)
            {
                Scene scene = _presentation;
                _presentation = default;
                return SceneManager.UnloadSceneAsync(scene);
            }
            RegisterLateLoadCleanup();
            return null;
        }

        private void RegisterLateLoadCleanup()
        {
            if (_loading == null || _loading.isDone || _loadCleanupRegistered)
            {
                return;
            }
            _loadCleanupRegistered = true;
            string path = _loadingPath;
            // The callback survives this component's destruction; a late load must never steal the screen.
            _loading.completed += operation =>
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        root.SetActive(false);
                    }
                    SceneManager.UnloadSceneAsync(scene);
                }
            };
        }

        private void Cleanup()
        {
            if (!_active)
            {
                return;
            }
            _active = false;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreVisuals();
            UnloadPresentation();
            foreach (KeyValuePair<Behaviour, bool> entry in _saved)
            {
                if (entry.Key != null)
                {
                    entry.Key.enabled = entry.Value;
                }
            }
            _saved.Clear();
            foreach (KeyValuePair<EventSystem, GameObject> entry in _selected)
            {
                if (entry.Key != null && entry.Key.isActiveAndEnabled && entry.Value != null && entry.Value.activeInHierarchy)
                {
                    entry.Key.SetSelectedGameObject(entry.Value);
                }
            }
            _selected.Clear();
            _loading = null;
            _loadCleanupRegistered = false;
            _camera = null;
            _director = null;
            _listener = null;
            CinematicPresentationLock.Release(this);
        }
    }
}
