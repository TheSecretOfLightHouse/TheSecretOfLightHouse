using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class MantaRiseAttack : NetworkBehaviour
    {
        [Header("수중 접근")]
        [SerializeField, Min(0.01f)] private float _submergeDepth = 3f;
        [SerializeField, Min(0.01f)] private float _approachSpeed = 6f;
        [SerializeField, Min(0.01f)] private float _alignmentRadius = 0.6f;
        [SerializeField] private float _fallbackWaterHeight;
        [Header("상승 타격")]
        [SerializeField, Min(0.01f)] private float _preparationSeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float _riseSpeed = 10f;
        [SerializeField, Min(0.01f)] private float _riseHeight = 1f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 1.2f;
        [SerializeField, Min(0.01f)] private float _diveSpeed = 6f;
        [SerializeField, Min(0.01f)] private float _timeoutSeconds = 20f;

        [SyncVar] private MantaAttackPhase _phase;
        [SyncVar] private double _phaseStartedAt;
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private Rigidbody _body;
        private IWorldQuery _world;
        private MonsterBoatTarget _target;
        private Vector3 _strikePosition;
        private Vector3 _previousPosition;
        private Vector3 _previousTargetPosition;
        private float _elapsed;
        private float _preparationElapsed;
        private bool _ownsMovement;
        private bool _hasAttemptedDamage;

        public MantaAttackPhase Phase => _phase;
        public double PhaseStartedAt => _phaseStartedAt;
        public bool IsAttacking => _ownsMovement;
        public bool HasCompleted { get; private set; }
        public bool HasHit { get; private set; }
        public bool CanAct => isServer && isActiveAndEnabled && _monster != null
            && _monster.IsInitialized && !_monster.IsDead && _monster.isActiveAndEnabled
            && _body != null && !_body.isKinematic && _motion != null && _motion.CanMove
            && _sensor != null && _sensor.isActiveAndEnabled && _monster.Profile != null
            && Positive(_monster.Profile.AttackDamage) && HasValidSettings();

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!isServer)
            {
                return;
            }

            if (!CanAct)
            {
                StopAttack(false);

                return;
            }

            if (!_ownsMovement)
            {
                if (!_motion.IsMovementLocked && TryWaterHeight(_body.position, out float water))
                {
                    Vector3 velocity = _body.linearVelocity;
                    velocity.y = Mathf.Clamp((water - _submergeDepth - _body.position.y) / Time.fixedDeltaTime,
                        -_diveSpeed, _diveSpeed);
                    _body.linearVelocity = velocity;
                }

                return;
            }
            _elapsed += Time.fixedDeltaTime;

            if (_elapsed >= _timeoutSeconds || !HasValidTarget())
            {
                StopAttack(false);

                return;
            }
            if (!TryWaterHeight(_target.transform.position, out float height))
            {
                StopAttack(false);

                return;
            }

            Vector3 position = _body.position;
            switch (_phase)
            {
                case MantaAttackPhase.Approaching:
                    Vector3 below = _target.transform.position;
                    below.y = height - _submergeDepth;

                    if (MoveTowards(below, _approachSpeed, _alignmentRadius))
                    {
                        _preparationElapsed = 0f;
                        SetPhase(MantaAttackPhase.Preparing);
                    }

                    break;

                case MantaAttackPhase.Preparing:
                    Vector3 preparation = _target.transform.position;
                    preparation.y = height - _submergeDepth;
                    MoveTowards(preparation, _approachSpeed, _alignmentRadius);
                    _preparationElapsed += Time.fixedDeltaTime;

                    if (_preparationElapsed >= _preparationSeconds)
                    {
                        // 이동하는 배가 피할 수 있도록 상승 시작 시 공격 위치를 고정한다.
                        _strikePosition = preparation;
                        _previousPosition = position;
                        _previousTargetPosition = _target.transform.position;
                        SetPhase(MantaAttackPhase.Rising);
                    }

                    break;

                case MantaAttackPhase.Rising:
                    TryStrike(position);

                    if (!_ownsMovement)
                    {
                        return;
                    }

                    Vector3 top = _strikePosition;
                    top.y = height + _riseHeight;

                    if (MoveTowards(top, _riseSpeed, _alignmentRadius))
                    {
                        SetPhase(MantaAttackPhase.Diving);
                    }

                    _previousPosition = position;
                    _previousTargetPosition = _target.transform.position;

                    break;

                case MantaAttackPhase.Diving:
                    Vector3 bottom = _strikePosition;
                    bottom.y = height - _submergeDepth;

                    if (MoveTowards(bottom, _diveSpeed, _alignmentRadius))
                    {
                        StopAttack(true);
                    }

                    break;
            }
        }

        private void OnDisable()
        {
            StopAttack(false);
        }

        public override void OnStopServer()
        {
            StopAttack(false);
            base.OnStopServer();
        }

        [Server]
        public bool BeginAttack(GameObject target)
        {
            if (!CanAct || _ownsMovement || _motion.IsMovementLocked || target == null
                || _monster.Profile == null || !Positive(_monster.Profile.AttackDamage))
            {
                return false;
            }

            _target = target.GetComponent<MonsterBoatTarget>();

            if (!HasValidTarget() || !TryWaterHeight(target.transform.position, out _))
            {
                _target = null;

                return false;
            }

            _elapsed = 0f;
            _hasAttemptedDamage = false;
            HasHit = false;
            HasCompleted = false;
            _ownsMovement = true;
            _motion.SetMovementLocked(true);
            _body.linearVelocity = Vector3.zero;
            SetPhase(MantaAttackPhase.Approaching);
            _monster.TrySetBehaviorState(MonsterState.Attack);

            return _ownsMovement;
        }

        [Server]
        public void Cancel()
        {
            StopAttack(false);
        }

        private void TryStrike(Vector3 current)
        {
            if (_hasAttemptedDamage || _target == null)
            {
                return;
            }

            Vector3 targetPosition = _target.transform.position;
            if (!MantaStrikeRules.TryGetCrossingOffset(_previousPosition - _previousTargetPosition, current - targetPosition, out Vector3 offset))
            {
                return;
            }

            _hasAttemptedDamage = true;
            float radius = _hitRadius + _target.HitRadius;

            if (MonsterMotion.IsFinite(offset) && Positive(radius) && offset.sqrMagnitude <= radius * radius)
            {
                // 피해 콜백에서 대상이 무효화되거나 공격이 취소될 수 있으므로 타격 시도를 먼저 기록한다.
                MonsterBoatTarget target = _target;
                bool hit = target.TryApplyDamage(_monster.Profile.AttackDamage);

                if (_ownsMovement)
                {
                    HasHit = hit;
                }
            }
        }

        private bool MoveTowards(Vector3 destination, float speed, float radius)
        {
            Vector3 offset = destination - _body.position;
            float distance = offset.magnitude;

            if (distance <= radius)
            {
                _body.linearVelocity = Vector3.zero;

                return true;
            }

            _body.linearVelocity = offset.normalized * Mathf.Min(speed, distance / Time.fixedDeltaTime);
            offset.y = 0f;

            if (offset.sqrMagnitude > 0.001f)
            {
                _body.MoveRotation(Quaternion.LookRotation(offset, Vector3.up));
            }

            return false;
        }

        private void StopAttack(bool completed)
        {
            if (!_ownsMovement)
            {
                return;
            }

            _ownsMovement = false;
            HasCompleted = completed;

            if (!completed)
            {
                HasHit = false;
            }

            _target = null;

            if (_motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }

            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
            }

            if (isServer)
            {
                SetPhase(MantaAttackPhase.Idle);
            }
        }

        private bool HasValidTarget()
        {
            return _target != null && _target.IsEligible
                && MonsterMotion.IsFinite(_target.transform.position)
                && _sensor.CanTrack(_target.gameObject, _monster.Profile.MaxChaseDistance);
        }

        private bool TryWaterHeight(Vector3 position, out float height)
        {
            // 등록된 월드 수면 조회를 우선 사용하고, 독립 시험 씬에서는 고정 수면 높이를 사용한다.
            if (_world == null)
            {
                WorldServices.TryGet(out _world);
            }

            height = _world == null ? _fallbackWaterHeight : _world.SampleWaterHeight(position);

            return !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private void SetPhase(MantaAttackPhase phase)
        {
            _phase = phase;
            _phaseStartedAt = NetworkTime.time;
        }

        private bool HasValidSettings()
        {
            return Positive(_submergeDepth) && Positive(_approachSpeed) && Positive(_alignmentRadius)
                && Positive(_preparationSeconds) && Positive(_riseSpeed) && Positive(_riseHeight)
                && Positive(_hitRadius) && Positive(_diveSpeed) && Positive(_timeoutSeconds)
                && _riseHeight > _alignmentRadius;
        }

        private static bool Positive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
