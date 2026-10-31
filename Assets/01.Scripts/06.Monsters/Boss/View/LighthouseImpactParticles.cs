using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20002)]
    [DisallowMultipleComponent]
    public sealed class LighthouseImpactParticles : MonoBehaviour
    {
        [SerializeField] private Transform _origin;
        [SerializeField] private Transform[] _impactPoints = new Transform[0];
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private ParticleSystem _fragments;
        [SerializeField, Min(0)] private int _dustCount = 26;
        [SerializeField, Min(0)] private int _fragmentCount = 16;
        [SerializeField, Min(1)] private int _pointsPerHit = 2;

        private BossTestEnvironment _boss;
        private TowerMatchNet _match;
        private float _previousHealth;
        private uint _sequence;
        private bool _hasHealth;

        public void Bind(BossTestEnvironment boss)
        {
            _boss = boss;
            _match = boss != null ? boss.TowerMatch : null;
            _hasHealth = false;
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !NetworkClient.ready || _boss == null || _match == null)
            {
                _hasHealth = false;
                return;
            }
            float health = _match.Health;
            if (!_hasHealth || _sequence != _match.CinematicSequence)
            {
                // Initial replication establishes a baseline instead of replaying old impacts.
                _previousHealth = health;
                _sequence = _match.CinematicSequence;
                _hasHealth = true;
                return;
            }
            if (health < _previousHealth && _boss.HasSummoned && !_boss.IsSpecialEnding
                && !_match.IsSummonCinematicPlaying && (_match.Phase == TowerPhase.FinalBattle
                    || (_match.Phase == TowerPhase.Ended && _match.EndReason == TowerEndReason.LighthouseDestroyed)))
            {
                PlayImpact(_boss.AttackSequence);
            }
            _previousHealth = health;
        }

        private void OnDisable()
        {
            _hasHealth = false;
            if (_dust != null)
            {
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_fragments != null)
            {
                _fragments.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void PlayImpact(uint attackSequence)
        {
            if (_origin == null || _impactPoints.Length == 0 || _match == null)
            {
                return;
            }
            _origin.SetPositionAndRotation(_match.transform.position, _match.transform.rotation);
            int count = Mathf.Clamp(_pointsPerHit, 1, _impactPoints.Length);
            int start = (int)(attackSequence % (uint)_impactPoints.Length);
            int step = _impactPoints.Length / count;
            for (int i = 0; i < count; i++)
            {
                Transform point = _impactPoints[(start + i * step) % _impactPoints.Length];
                if (point == null)
                {
                    continue;
                }
                if (_dust != null)
                {
                    _dust.transform.SetPositionAndRotation(point.position, point.rotation);
                    _dust.Emit(_dustCount);
                }
                if (_fragments != null)
                {
                    _fragments.transform.SetPositionAndRotation(point.position, point.rotation);
                    _fragments.Emit(_fragmentCount);
                }
            }
        }
    }
}
