using System.Collections.Generic;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossTestEnvironment))]
    public sealed class WhipTentacleBattleNet : NetworkBehaviour
    {
        private sealed class AttackRuntime
        {
            public readonly HashSet<MonsterBoatTarget> HitTargets = new HashSet<MonsterBoatTarget>();
            public int LastFrame = -1;
            public float Damage;
        }

        [SerializeField] private WhipTentacleSettings _settings;
        [SerializeField, Min(1)] private int _tentacleCount = 5;

        [SyncVar] private bool _hasStarted;
        [SyncVar] private bool _isStopped;
        [SyncVar] private double _combatStartedAt;
        [SyncVar] private double _stoppedAt;
        private readonly SyncList<WhipTentacleAttackState> _attacks = new SyncList<WhipTentacleAttackState>();
        private readonly Dictionary<MonsterBoatTarget, Collider[]> _targetColliders = new Dictionary<MonsterBoatTarget, Collider[]>();
        private WhipTentaclePresentation _presentation;
        private BossTestEnvironment _boss;
        private TowerMatchNet _match;
        private AttackRuntime[] _runtime;

        public bool HasStarted => _hasStarted;
        public bool IsStopped => _isStopped;
        public double CombatStartedAt => _combatStartedAt;
        public double StoppedAt => _stoppedAt;
        public int AttackCount => _attacks.Count;

        private void Awake()
        {
            _boss = GetComponent<BossTestEnvironment>();
            _match = GetComponent<TowerMatchNet>();
        }

        private void Update()
        {
            if (netIdentity == null || !isServer || _isStopped || _runtime == null)
            {
                return;
            }
            if (_settings == null || !_settings.IsValid)
            {
                StopBattle();
                return;
            }
            if (_match == null || _match.Phase == TowerPhase.Ended || _boss.Phase == BossPhase.BattleStopped
                || _boss.Phase == BossPhase.LighthouseDestroyed
                || (_hasStarted && (_presentation == null || !_presentation.isActiveAndEnabled)))
            {
                StopBattle();
                return;
            }
            if (_match.Phase != TowerPhase.FinalBattle || _match.IsSummonCinematicPlaying
                || _boss.Phase != BossPhase.NormalBattleActive || _presentation == null)
            {
                return;
            }
            double now = NetworkTime.time;
            if (!_hasStarted)
            {
                _combatStartedAt = now;
                _hasStarted = true;
            }
            for (int i = 0; i < _attacks.Count; i++)
            {
                WhipTentacleAttackState state = _attacks[i];
                double elapsed = (now - state.StartedAt) * state.Speed;
                if (state.Sequence > 0)
                {
                    EvaluateHitFrames(i, Mathf.Clamp01((float)(elapsed / _settings.AttackClip.length)));
                    if (_isStopped)
                    {
                        return;
                    }
                    if (elapsed < _settings.AttackClip.length)
                    {
                        continue;
                    }
                }
                if (HasTarget(i))
                {
                    state.Sequence++;
                    state.StartedAt = now;
                    state.Speed = _settings.AttackSpeed;
                    _attacks[i] = state;
                    _runtime[i].LastFrame = -1;
                    _runtime[i].Damage = _settings.Damage;
                    _runtime[i].HitTargets.Clear();
                }
            }
        }

        private void OnDisable()
        {
            DetachTargets();
            if (netIdentity != null && isServer)
            {
                StopBattle();
            }
        }

        public override void OnStartServer()
        {
            if (_settings == null || !_settings.IsValid || _match == null)
            {
                Debug.LogError($"[{nameof(WhipTentacleBattleNet)}] Assign valid attack settings and tower match.", this);
                return;
            }
            _hasStarted = false;
            _isStopped = false;
            _attacks.Clear();
            _runtime = new AttackRuntime[_tentacleCount];
            for (int i = 0; i < _runtime.Length; i++)
            {
                _runtime[i] = new AttackRuntime();
                _attacks.Add(default);
            }
            _match.Ended += HandleMatchEnded;
            MonsterBoatTarget.TargetRegistered += HandleTargetRegistered;
            MonsterBoatTarget.TargetUnregistered += HandleTargetUnregistered;
            IReadOnlyList<MonsterBoatTarget> targets = MonsterBoatTarget.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                HandleTargetRegistered(targets[i]);
            }
        }

        public override void OnStopServer()
        {
            if (_match != null)
            {
                _match.Ended -= HandleMatchEnded;
            }
            DetachTargets();
            _runtime = null;
            _targetColliders.Clear();
            _attacks.Clear();
            _hasStarted = false;
        }

        public WhipTentacleAttackState GetAttack(int index)
        {
            return _attacks[index];
        }

        public void BindPresentation(WhipTentaclePresentation presentation)
        {
            if (presentation != null && presentation.IsReady && presentation.Count == _tentacleCount
                && presentation.Settings == _settings)
            {
                _presentation = presentation;
            }
        }

        private void HandleTargetRegistered(MonsterBoatTarget target)
        {
            if (target != null && !_targetColliders.ContainsKey(target))
            {
                _targetColliders.Add(target, target.GetComponentsInChildren<Collider>(true));
            }
        }

        private void HandleTargetUnregistered(MonsterBoatTarget target)
        {
            _targetColliders.Remove(target);
        }

        private void DetachTargets()
        {
            MonsterBoatTarget.TargetRegistered -= HandleTargetRegistered;
            MonsterBoatTarget.TargetUnregistered -= HandleTargetUnregistered;
            _targetColliders.Clear();
        }

        private bool HasTarget(int index)
        {
            BoxCollider box = _presentation.GetHitBox(index);
            Bounds bounds = _settings.AttackEnvelope;
            IReadOnlyList<MonsterBoatTarget> targets = MonsterBoatTarget.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                MonsterBoatTarget target = targets[i];
                if (target != null && target.IsEligible && Overlaps(box, bounds, target))
                {
                    return true;
                }
            }
            return false;
        }

        private void EvaluateHitFrames(int index, float normalizedTime)
        {
            AttackRuntime runtime = _runtime[index];
            int last = Mathf.FloorToInt(normalizedTime * (_settings.FrameCount - 1));
            for (int frame = runtime.LastFrame + 1; frame <= last; frame++)
            {
                float fraction = frame / (float)(_settings.FrameCount - 1);
                if (fraction < _settings.HitStart || fraction > _settings.HitEnd)
                {
                    continue;
                }
                Bounds bounds = _settings.GetFrame(frame);
                // Include the path between baked frames so a fast strike cannot skip a boat.
                if (frame > 0)
                {
                    bounds.Encapsulate(_settings.GetFrame(frame - 1));
                }
                BoxCollider box = _presentation.GetHitBox(index);
                IReadOnlyList<MonsterBoatTarget> targets = MonsterBoatTarget.Targets;
                for (int t = targets.Count - 1; t >= 0; t--)
                {
                    MonsterBoatTarget target = targets[t];
                    if (target == null || !target.IsEligible || runtime.HitTargets.Contains(target)
                        || !Overlaps(box, bounds, target))
                    {
                        continue;
                    }
                    // Safety zones deliberately do not filter this boss attack.
                    runtime.HitTargets.Add(target);
                    target.TryApplyDamage(runtime.Damage);
                    if (_isStopped)
                    {
                        return;
                    }
                }
            }
            runtime.LastFrame = last;
        }

        private bool Overlaps(BoxCollider box, Bounds bounds, MonsterBoatTarget target)
        {
            if (!_targetColliders.TryGetValue(target, out Collider[] colliders))
            {
                return false;
            }
            box.center = bounds.center;
            box.size = bounds.size;
            // PhysX needs an enabled shape, but these query boxes must not enter a physics step.
            box.enabled = true;
            try
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
                        && Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                            collider, collider.transform.position, collider.transform.rotation, out _, out _))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                box.enabled = false;
            }
            return false;
        }

        [Server]
        private void HandleMatchEnded(TowerOutcome outcome, TowerEndReason reason)
        {
            StopBattle();
        }

        private void StopBattle()
        {
            if (!_isStopped)
            {
                _stoppedAt = NetworkTime.time;
                _isStopped = true;
            }
        }
    }
}
