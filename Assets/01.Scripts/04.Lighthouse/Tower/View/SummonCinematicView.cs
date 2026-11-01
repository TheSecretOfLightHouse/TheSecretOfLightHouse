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
        private MainSceneSummonPresentation _specialRig;
        private Lighthouse.Monsters.Boss.BossTestEnvironment _boss;
        private Transform _island;
        private bool _ownsRig;
        private bool _ownsSpecialRig;
        private bool _isSpecial;
        private uint _sequence;
        private bool _active;
        private bool _showing;
        private float _nextScanAt;

        private MainSceneSummonPresentation CurrentRig => _isSpecial ? _specialRig : _rig;

        public void Initialize(TowerMatchNet match)
        {
            _match = match;
            _boss = match != null ? match.GetComponent<Lighthouse.Monsters.Boss.BossTestEnvironment>() : null;
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
            if (_ownsSpecialRig && _specialRig != null)
            {
                Destroy(_specialRig.gameObject);
            }
            _island = island;
            _rig = null;
            _specialRig = null;
            foreach (MainSceneSummonPresentation candidate in island.GetComponentsInChildren<MainSceneSummonPresentation>(true))
            {
                if (candidate.GetComponent<Lighthouse.Monsters.Boss.SpecialEndingIslandBinding>() != null)
                {
                    _specialRig = candidate;
                }
                else
                {
                    _rig = candidate;
                }
            }
            _ownsRig = false;
            _ownsSpecialRig = false;
            if (_rig == null && _match != null && _match.Settings != null && _match.Settings.SummonPresentationPrefab != null)
            {
                GameObject instance = Instantiate(_match.Settings.SummonPresentationPrefab, island, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                _rig = instance.GetComponent<MainSceneSummonPresentation>();
                _ownsRig = true;
            }
            if (_specialRig == null && _match != null && _match.Settings != null
                && _match.Settings.SpecialEndingPresentationPrefab != null)
            {
                GameObject instance = Instantiate(_match.Settings.SpecialEndingPresentationPrefab, island, false);
                _specialRig = instance.GetComponent<MainSceneSummonPresentation>();
                _ownsSpecialRig = true;
            }
            if (_specialRig != null)
            {
                var binding = _specialRig.GetComponent<Lighthouse.Monsters.Boss.SpecialEndingIslandBinding>();
                if (binding != null)
                {
                    binding.Bind(island);
                }
            }
            if (_rig != null && _match != null && _match.CinematicPhase == SummonCinematicPhase.Combat)
            {
                _isSpecial = _boss != null && _boss.IsSpecialEnding;
                if (CurrentRig != null)
                {
                    CurrentRig.Finish(true);
                }
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
            if (_ownsSpecialRig && _specialRig != null)
            {
                Destroy(_specialRig.gameObject);
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
            // Tower and boss SyncVars can arrive in separate messages; wait for the public branch decision.
            while (IsCurrent && _boss != null && !_boss.HasSummoned
                && NetworkTime.time < _match.CinematicLoadDeadline)
            {
                yield return null;
            }
            _isSpecial = _boss != null && _boss.IsSpecialEnding;
            MainSceneSummonPresentation rig = CurrentRig;
            while (IsCurrent && (rig == null || !rig.IsReady)
                && _match.CinematicPhase == SummonCinematicPhase.Preparing
                && NetworkTime.time < _match.CinematicLoadDeadline)
            {
                yield return null;
                rig = CurrentRig;
            }
            if (!IsCurrent)
            {
                yield break;
            }
            bool skipped = rig == null || !rig.IsReady;
            if (!skipped)
            {
                rig.MatchCamera(Camera.main);
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
                        rig.Synchronize(elapsed);
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
                    if ((_rig != null && behaviour.transform.IsChildOf(_rig.transform))
                        || (_specialRig != null && behaviour.transform.IsChildOf(_specialRig.transform)))
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
            if (CurrentRig != null)
            {
                bool completed = _match != null && (_match.CinematicPhase == SummonCinematicPhase.Returning
                    || _match.CinematicPhase == SummonCinematicPhase.Combat);
                CurrentRig.Finish(completed);
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
