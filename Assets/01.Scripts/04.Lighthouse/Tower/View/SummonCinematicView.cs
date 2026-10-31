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
        private MainSceneSummonPresentation _rig;
        private Transform _island;
        private bool _ownsRig;
        private uint _sequence;
        private bool _active;
        private bool _showing;
        private float _nextScanAt;

        public void Initialize(TowerMatchNet match)
        {
            _match = match;
        }

        /// <summary>Binds the presentation to the actual locally constructed island.</summary>
        public void BindIsland(Transform island)
        {
            if (island == null || (_island == island && _rig != null))
            {
                return;
            }
            Cleanup();
            if (_ownsRig && _rig != null)
            {
                Destroy(_rig.gameObject);
            }
            _island = island;
            _rig = island.GetComponentInChildren<MainSceneSummonPresentation>(true);
            _ownsRig = false;
            if (_rig == null && _match != null && _match.Settings != null && _match.Settings.SummonPresentationPrefab != null)
            {
                GameObject instance = Instantiate(_match.Settings.SummonPresentationPrefab, island, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                _rig = instance.GetComponent<MainSceneSummonPresentation>();
                _ownsRig = true;
            }
            if (_rig != null && _match != null && _match.CinematicPhase == SummonCinematicPhase.Combat)
            {
                _rig.Finish(true);
            }
            if (_rig != null && _match != null)
            {
                var combat = _rig.GetComponent<Lighthouse.Monsters.Boss.SummonTentacleCombatView>();
                if (combat != null)
                {
                    combat.Bind(_match.GetComponent<Lighthouse.Monsters.Boss.BossTestEnvironment>());
                }
                var whips = _rig.GetComponent<Lighthouse.Monsters.Boss.WhipTentaclePresentation>();
                if (whips != null)
                {
                    whips.Bind(_match.GetComponent<Lighthouse.Monsters.Boss.WhipTentacleBattleNet>());
                }
            }
        }

        private void OnDestroy()
        {
            Cleanup();
            if (_ownsRig && _rig != null)
            {
                Destroy(_rig.gameObject);
            }
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
            while (IsCurrent && (_rig == null || !_rig.IsReady)
                && _match.CinematicPhase == SummonCinematicPhase.Preparing
                && NetworkTime.time < _match.CinematicLoadDeadline)
            {
                yield return null;
            }
            if (!IsCurrent)
            {
                yield break;
            }
            bool skipped = _rig == null || !_rig.IsReady;
            if (!skipped)
            {
                _rig.MatchCamera(Camera.main);
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
                    try
                    {
                        if (!_showing)
                        {
                            CaptureScenes();
                            _showing = true;
                        }
                        _rig.Synchronize(elapsed);
                    }
                    catch (Exception)
                    {
                        skipped = true;
                        RestoreVisuals();
                    }
                }
                yield return null;
            }
            RestoreVisuals();
            if (IsCurrent)
            {
                _match.CmdReportCinematicReturned(_sequence);
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
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }
            scene.GetRootGameObjects(_roots);
            foreach (GameObject root in _roots)
            {
                root.GetComponentsInChildren(true, _behaviours);
                foreach (Behaviour behaviour in _behaviours)
                {
                    // A component whose script is missing comes back null, e.g. a dev-only script left out of a release build.
                    if (behaviour == null)
                    {
                        continue;
                    }
                    if (_rig != null && behaviour.transform.IsChildOf(_rig.transform))
                    {
                        continue;
                    }
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
            if (_rig != null)
            {
                bool completed = _match != null && (_match.CinematicPhase == SummonCinematicPhase.Returning
                    || _match.CinematicPhase == SummonCinematicPhase.Combat);
                _rig.Finish(completed);
            }
            foreach (KeyValuePair<Behaviour, bool> entry in _saved)
            {
                if (entry.Key != null && !(entry.Key is EventSystem))
                {
                    entry.Key.enabled = entry.Value;
                }
            }
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
            CinematicPresentationLock.Release(this);
        }
    }
}
