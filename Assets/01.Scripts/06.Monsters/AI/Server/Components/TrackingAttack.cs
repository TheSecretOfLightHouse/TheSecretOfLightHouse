using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class TrackingAttack : MonoBehaviour
    {
        [Header("추적 돌진 설정")]
        [SerializeField, Min(0.01f)] private float _speed = 18f;
        [SerializeField, Min(0.01f)] private float _acceleration = 9f;
        [SerializeField, Min(0.01f)] private float _hitRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float _timeout = 10f;

        [Header("공격 후 이탈 설정")]
        [SerializeField, Min(0.01f)] private float _retreatDistance = 15f;
        [SerializeField, Min(0.01f)] private float _retreatSpeed = 15f;
        [SerializeField, Min(0.01f)] private float _retreatTimeout = 15f;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private BlueSharkAnimation _animation;
        private MonsterBoatTarget _target;
        private Vector3 _previousOffset;
        private float _elapsedTime;
        private float _currentSpeed;
        private bool _isAttacking;
        private bool _hasSucceeded;

        public bool IsAttacking => _isAttacking;
        public bool HasSucceeded => _hasSucceeded;
        public float RetreatDistance => _retreatDistance;
        public float RetreatSpeed => _retreatSpeed;
        public float RetreatTimeout => _retreatTimeout;
        public bool HasValidSettings => IsPositive(_speed) && IsPositive(_acceleration)
            && IsPositive(_hitRadius) && IsPositive(_timeout);
        public bool HasValidRetreatSettings => IsPositive(_retreatDistance)
            && IsPositive(_retreatSpeed) && IsPositive(_retreatTimeout);
        public bool CanAct => isActiveAndEnabled && _monster != null && _motion != null
            && _sensor != null && _motion.CanMove && _motion.HasValidSetup
            && !_motion.IsMovementLocked && _sensor.isActiveAndEnabled && HasValidSettings;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _animation = GetComponent<BlueSharkAnimation>();
        }

        private void FixedUpdate()
        {
            if (_isAttacking)
            {
                StepAttack(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            Cancel();
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
            _previousOffset = GetTargetOffset();
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
            if (!CanAct || !HasValidTarget() || _monster.Profile == null
                || !IsPositive(_monster.Profile.AttackDamage))
            {
                FinishAttack(false);
                return;
            }

            Vector3 offset = GetTargetOffset();
            float radius = _hitRadius + _target.HitRadius;
            Vector3 segment = offset - _previousOffset;
            float fraction = segment.sqrMagnitude > 0f
                ? Mathf.Clamp01(-Vector3.Dot(_previousOffset, segment) / segment.sqrMagnitude) : 0f;
            Vector3 closest = _previousOffset + segment * fraction;
            _previousOffset = offset;

            // 배나 샤크가 빠르게 이동해도 명중을 놓치지 않도록 상대 이동 구간 전체를 검사한다.
            if (closest.sqrMagnitude <= radius * radius)
            {
                // 피해 콜백 중 다음 물리 갱신에서 피해를 중복 적용하지 않도록 먼저 공격을 종료한다.
                _isAttacking = false;
                _motion.Stop();
                bool succeeded = _target.TryApplyDamage(_monster.Profile.AttackDamage);
                FinishAttack(succeeded);
                return;
            }

            _elapsedTime += deltaTime;
            if (_elapsedTime >= _timeout)
            {
                FinishAttack(false);
                return;
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed, _acceleration * deltaTime);
            if (_currentSpeed > 0f && !_motion.SetChargeVelocity(offset, _currentSpeed))
            {
                FinishAttack(false);
            }
        }

        private void FinishAttack(bool succeeded)
        {
            _isAttacking = false;
            _hasSucceeded = succeeded;
            _target = null;
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

        private bool HasValidTarget()
        {
            if (_target == null || !_target.IsEligible || !IsPositive(_target.HitRadius)
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
