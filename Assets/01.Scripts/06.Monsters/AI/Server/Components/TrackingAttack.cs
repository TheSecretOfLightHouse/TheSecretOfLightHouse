using Lighthouse.Monsters.AI.Net;
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
                StepAttack(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            Cancel();
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

        private void HandleCollision(Collision collision)
        {
            if (!_isAttacking || !CanAct || !HasValidTarget() || collision.collider == null
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

                // 피해 콜백이나 추가 접촉이 같은 공격의 피해를 중복 적용하지 않도록 먼저 종료한다.
                _isAttacking = false;
                _motion.Stop();
                bool succeeded = _target.TryApplyDamage(_monster.Profile.AttackDamage);
                FinishAttack(succeeded);
                return;
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
            if (_target == null || !_target.IsEligible
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
