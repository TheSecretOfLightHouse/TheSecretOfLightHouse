using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "배를 추적하며 돌진 공격",
        story: "[Self]가 [TargetBoat]를 실시간 추적하며 돌진해 한 번 공격한다",
        category: "행동/등대/몬스터", id: "0fa431ebac284ecbb26b0bc41fe4ed5a")]
    public sealed class TrackingChargeAction : MonsterAction
    {
        [SerializeReference] private BlackboardVariable<float> _speed = new BlackboardVariable<float>(18f);
        [SerializeReference] private BlackboardVariable<float> _acceleration = new BlackboardVariable<float>(9f);
        [SerializeReference] private BlackboardVariable<float> _hitRadius = new BlackboardVariable<float>(0.65f);
        [SerializeReference] private BlackboardVariable<float> _timeout = new BlackboardVariable<float>(10f);

        [NonSerialized] private MonsterBoatTarget _target;
        [NonSerialized] private Vector3 _previousOffset;
        [NonSerialized] private float _elapsedTime;
        [NonSerialized] private float _currentSpeed;
        [NonSerialized] private bool _hasAttacked;

        public BlackboardVariable<float> Speed { get => _speed; set => _speed = value; }
        public BlackboardVariable<float> Acceleration { get => _acceleration; set => _acceleration = value; }
        public BlackboardVariable<float> HitRadius { get => _hitRadius; set => _hitRadius = value; }
        public BlackboardVariable<float> Timeout { get => _timeout; set => _timeout = value; }

        protected override Status OnStart()
        {
            _elapsedTime = 0f;
            _currentSpeed = 0f;
            _hasAttacked = false;
            _target = null;
            if (!ResolveReferences() || !HasValidSettings() || TargetBoat.Value == null)
            {
                return Status.Failure;
            }

            _target = TargetBoat.Value.GetComponent<MonsterBoatTarget>();
            if (!HasValidTarget())
            {
                return Status.Failure;
            }

            _previousOffset = GetTargetOffset();
            Motion.Stop();
            Monster.TrySetBehaviorState(MonsterState.Attack);
            return StepAttack(0f);
        }

        protected override Status OnUpdate()
        {
            return StepAttack(Time.deltaTime);
        }

        private Status StepAttack(float deltaTime)
        {
            if (_hasAttacked)
            {
                return Status.Success;
            }
            if (!CanAct || Motion.IsMovementLocked || !HasValidSettings() || !HasValidTarget())
            {
                return Status.Failure;
            }

            Vector3 offset = GetTargetOffset();
            float radius = _hitRadius.Value + _target.HitRadius;
            Vector3 segment = offset - _previousOffset;
            float fraction = segment.sqrMagnitude > 0f
                ? Mathf.Clamp01(-Vector3.Dot(_previousOffset, segment) / segment.sqrMagnitude) : 0f;
            Vector3 closest = _previousOffset + segment * fraction;
            _previousOffset = offset;

            // 배나 몬스터가 빠르게 이동해도 명중을 놓치지 않도록 상대 이동 구간 전체를 검사한다.
            if (closest.sqrMagnitude <= radius * radius)
            {
                _hasAttacked = true;
                Motion.Stop();
                return _target.TryApplyDamage(Monster.Profile.AttackDamage) ? Status.Success : Status.Failure;
            }

            _elapsedTime += deltaTime;
            if (_elapsedTime >= _timeout.Value)
            {
                return Status.Failure;
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed.Value, _acceleration.Value * deltaTime);
            if (_currentSpeed <= 0f)
            {
                return Status.Running;
            }

            return Motion.SetChargeVelocity(offset, _currentSpeed) ? Status.Running : Status.Failure;
        }

        private bool HasValidTarget()
        {
            if (_target == null || TargetBoat.Value != _target.gameObject || !_target.IsEligible
                || !IsPositive(_target.HitRadius) || !MonsterMotion.IsFinite(_target.transform.position))
            {
                return false;
            }
            for (int i = 0; i < Sensor.Targets.Count; i++)
            {
                if (Sensor.Targets[i] == _target)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasValidSettings()
        {
            return _speed != null && _acceleration != null && IsPositive(_acceleration.Value)
                && _hitRadius != null && _timeout != null
                && IsPositive(_speed.Value) && IsPositive(_hitRadius.Value) && IsPositive(_timeout.Value)
                && Monster.Profile != null && IsPositive(Monster.Profile.AttackDamage);
        }

        private Vector3 GetTargetOffset()
        {
            Vector3 offset = _target.transform.position - Self.Value.transform.position;
            offset.y = 0f;
            return offset;
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
