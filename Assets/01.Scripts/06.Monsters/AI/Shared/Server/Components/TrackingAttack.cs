using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class TrackingAttack : MonoBehaviour
    {
        [Header("추적 돌진 설정")]
        [SerializeField, Min(0.01f)] private float _speed = 18f;
        [SerializeField, Min(0.01f)] private float _acceleration = 9f;
        [SerializeField, Min(0.01f)] private float _timeout = 10f;
        [SerializeField, Min(0.01f)] private float _biteContactTime = 0.63f;

        [Header("공격 후 이탈 설정")]
        [SerializeField, Min(0.01f)] private float _retreatDistance = 15f;
        [SerializeField, Min(0.01f)] private float _retreatSpeed = 15f;
        [SerializeField, Min(0.01f)] private float _retreatTimeout = 15f;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private BlueSharkAnimation _animation;
        private CapsuleCollider _bodyCollider;
        private MonsterBoatTarget _target;
        private Collider[] _targetColliders;
        private float _elapsedTime;
        private float _currentSpeed;
        private bool _isAttacking;
        private bool _hasSucceeded;
        private bool _wasInterrupted;
        private bool _isBiteHolding;
        private bool _hasBiteStarted;
        private double _biteStartedAt;
        private double _biteEndsAt;
        private Vector3 _previousTargetPosition;
        private float _lastHealth;

        public bool IsAttacking => _isAttacking;
        public bool HasSucceeded => _hasSucceeded;
        public bool WasInterrupted => _wasInterrupted;
        public float RetreatDistance => _retreatDistance;
        public float RetreatSpeed => _retreatSpeed;
        public float RetreatTimeout => _retreatTimeout;
        public bool HasValidSettings => IsPositive(_speed) && IsPositive(_acceleration)
            && IsPositive(_timeout);
        public bool HasValidRetreatSettings => IsPositive(_retreatDistance)
            && IsPositive(_retreatSpeed) && IsPositive(_retreatTimeout);
        public bool CanAct => isActiveAndEnabled && _monster != null && _motion != null
            && _sensor != null && _bodyCollider != null && _bodyCollider.enabled
            && !_bodyCollider.isTrigger && _motion.CanMove && _motion.HasValidSetup
            && !_motion.IsMovementLocked && _sensor.isActiveAndEnabled && HasValidSettings;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _animation = GetComponent<BlueSharkAnimation>();
            _bodyCollider = GetComponent<CapsuleCollider>();
        }

        private void FixedUpdate()
        {
            if (_isAttacking)
            {
                if (_isBiteHolding)
                {
                    if (_target != null && _target.IsInSafeZone)
                    {
                        CancelProtectedTarget();
                    }
                    else if (!CanAct)
                    {
                        FinishAttack(false);
                    }
                    else
                    {
                        if (NetworkTime.time >= _biteEndsAt)
                        {
                            FinishAttack(true);
                        }
                    }
                }
                else
                {
                    StepAttack(Time.fixedDeltaTime);
                }
            }
        }

        private void OnEnable()
        {
            if (_monster != null)
            {
                _lastHealth = _monster.CurrentHealth;
                _monster.HealthChanged += HandleHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (_monster != null)
            {
                _monster.HealthChanged -= HandleHealthChanged;
            }
            Cancel();
        }

        private void HandleHealthChanged(float health)
        {
            bool tookDamage = health < _lastHealth;
            _lastHealth = health;
            if (!tookDamage || !_isAttacking || _isBiteHolding || _monster == null
                || !_monster.isServer || _monster.IsDead)
            {
                return;
            }

            _wasInterrupted = true;
            FinishAttack(false);
            if (_animation != null)
            {
                _animation.CancelBite();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            // 공격 시작 전부터 접촉 중인 배도 같은 캡슐 접촉 기준으로 판정한다.
            HandleCollision(collision);
        }

        public bool BeginAttack(GameObject target)
        {
            if (!CanAct || _isAttacking || target == null
                || _monster.Profile == null || !IsPositive(_monster.Profile.AttackDamage))
            {
                return false;
            }

            _target = target.GetComponent<MonsterBoatTarget>();
            if (!HasValidTarget())
            {
                _target = null;
                return false;
            }

            _elapsedTime = 0f;
            _currentSpeed = 0f;
            _hasSucceeded = false;
            _wasInterrupted = false;
            _lastHealth = _monster.CurrentHealth;
            _isBiteHolding = false;
            _hasBiteStarted = false;
            _targetColliders = _target.GetComponentsInChildren<Collider>();
            _previousTargetPosition = _target.transform.position;
            _motion.Stop();
            _isAttacking = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);
            // 상태 변경 콜백에서 그래프를 중단하거나 이 컴포넌트를 비활성화할 수 있다.
            if (!_isAttacking || !CanAct)
            {
                Cancel();
                return false;
            }
            if (_animation != null)
            {
                _animation.SetAnimationState(BlueSharkAnimation.DashAttack);
            }
            StepAttack(0f);
            return true;
        }

        public void Cancel()
        {
            if (!_isAttacking)
            {
                return;
            }

            FinishAttack(false);
        }

        private void StepAttack(float deltaTime)
        {
            if (_target != null && _target.IsInSafeZone)
            {
                CancelProtectedTarget();
                return;
            }
            if (!CanAct || !HasValidTarget() || _monster.Profile == null
                || !IsPositive(_monster.Profile.AttackDamage))
            {
                FinishAttack(false);
                return;
            }

            Vector3 offset = GetTargetOffset();
            _elapsedTime += deltaTime;
            if (_elapsedTime >= _timeout)
            {
                FinishAttack(false);
                return;
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed, _acceleration * deltaTime);
            TryStartBite(offset, deltaTime);
            if (_currentSpeed > 0f && !_motion.SetChargeVelocity(offset, _currentSpeed))
            {
                FinishAttack(false);
            }
        }

        private void HandleCollision(Collision collision)
        {
            if (!_isAttacking || _isBiteHolding || !CanAct || !HasValidTarget() || collision.collider == null
                || _monster.Profile == null || !IsPositive(_monster.Profile.AttackDamage)
                || collision.collider.GetComponentInParent<MonsterBoatTarget>() != _target)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).thisCollider != _bodyCollider)
                {
                    continue;
                }

                // Block repeated contact while damage callbacks may cancel this attack.
                _isBiteHolding = true;
                _motion.Stop();
                bool succeeded = _target.TryApplyDamage(_monster.Profile.AttackDamage);
                if (!_isAttacking)
                {
                    return;
                }
                if (succeeded && _hasBiteStarted && CanAct
                    && NetworkTime.time < _biteEndsAt)
                {
                    return;
                }
                FinishAttack(succeeded);
                return;
            }
        }

        private void FinishAttack(bool succeeded)
        {
            _isAttacking = false;
            _isBiteHolding = false;
            _hasSucceeded = succeeded;
            _target = null;
            _targetColliders = null;
            if (_monster == null || !_monster.isServer)
            {
                return;
            }
            if (_motion != null)
            {
                _motion.Stop();
            }
            if (_animation != null)
            {
                _animation.SetAnimationState(BlueSharkAnimation.Idle);
            }
        }

        private void CancelProtectedTarget()
        {
            FinishAttack(false);
            if (_animation != null)
            {
                _animation.CancelBite();
            }
        }

        private void TryStartBite(Vector3 offset, float deltaTime)
        {
            Vector3 targetPosition = _target.transform.position;
            Vector3 targetVelocity = deltaTime > 0f
                ? (targetPosition - _previousTargetPosition) / deltaTime : Vector3.zero;
            _previousTargetPosition = targetPosition;
            if (_hasBiteStarted || _animation == null || _animation.BiteDuration <= 0f
                || _targetColliders == null || !MonsterMotion.IsFinite(targetVelocity))
            {
                return;
            }

            float nearestDistance = float.PositiveInfinity;
            Vector3 capsuleCenter = _bodyCollider.bounds.center;
            foreach (Collider collider in _targetColliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger
                    || !collider.gameObject.activeInHierarchy
                    || Physics.GetIgnoreLayerCollision(_bodyCollider.gameObject.layer, collider.gameObject.layer)
                    || Physics.GetIgnoreCollision(_bodyCollider, collider))
                {
                    continue;
                }

                Vector3 boatPoint = collider.ClosestPoint(capsuleCenter);
                Vector3 sharkPoint = _bodyCollider.ClosestPoint(boatPoint);
                boatPoint = collider.ClosestPoint(sharkPoint);
                sharkPoint = _bodyCollider.ClosestPoint(boatPoint);
                nearestDistance = Mathf.Min(nearestDistance, Vector3.Distance(sharkPoint, boatPoint));
            }

            offset.y = 0f;
            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.zero;
            targetVelocity.y = 0f;
            float closingSpeed = Mathf.Max(0f, _currentSpeed - Vector3.Dot(targetVelocity, direction));
            float leadTime = Mathf.Min(_biteContactTime, _animation.BiteDuration);
            float accelerationTime = Mathf.Min(leadTime, Mathf.Max(0f, (_speed - _currentSpeed) / _acceleration));
            float leadDistance = closingSpeed * leadTime
                + _acceleration * accelerationTime * (leadTime - 0.5f * accelerationTime);
            if (nearestDistance <= leadDistance + 0.05f)
            {
                _hasBiteStarted = true;
                _biteStartedAt = NetworkTime.time;
                _biteEndsAt = _biteStartedAt + _animation.BiteDuration;
                _animation.PlayBite();
            }
        }

        private bool HasValidTarget()
        {
            if (_target == null || !_target.IsEligible || _target.IsInSafeZone
                || !MonsterMotion.IsFinite(_target.transform.position))
            {
                return false;
            }
            for (int i = 0; i < _sensor.Targets.Count; i++)
            {
                if (_sensor.Targets[i] == _target)
                {
                    return true;
                }
            }
            return false;
        }

        private Vector3 GetTargetOffset()
        {
            Vector3 offset = _target.transform.position - transform.position;
            offset.y = 0f;
            return offset;
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
