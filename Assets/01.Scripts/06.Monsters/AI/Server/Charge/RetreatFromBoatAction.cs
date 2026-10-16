using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "공격 후 이탈 지점까지 이동",
        story: "[Self]가 [TargetBoat]의 반대 방향에 정한 이탈 지점까지 이동한다",
        category: "행동/등대/몬스터", id: "37b4d241a87945cdb89ef0e239ad1e77")]
    public sealed class RetreatFromBoatAction : MonsterAction
    {
        [SerializeReference] private BlackboardVariable<float> _distance = new BlackboardVariable<float>(15f);
        [SerializeReference] private BlackboardVariable<float> _speed = new BlackboardVariable<float>(8f);
        [SerializeReference] private BlackboardVariable<float> _timeout = new BlackboardVariable<float>(15f);

        [NonSerialized] private float _elapsedTime;

        public BlackboardVariable<float> Distance { get => _distance; set => _distance = value; }
        public BlackboardVariable<float> Speed { get => _speed; set => _speed = value; }
        public BlackboardVariable<float> Timeout { get => _timeout; set => _timeout = value; }

        protected override Status OnStart()
        {
            _elapsedTime = 0f;
            if (!ResolveReferences() || Motion.IsMovementLocked || !HasValidSettings())
            {
                return Status.Failure;
            }

            Vector3 position = Self.Value.transform.position;
            Vector3 direction = Self.Value.transform.forward;
            if (TargetBoat.Value != null)
            {
                if (!MonsterMotion.IsFinite(TargetBoat.Value.transform.position))
                {
                    return Status.Failure;
                }
                direction = position - TargetBoat.Value.transform.position;
            }
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -Self.Value.transform.forward;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            // 배가 이동하거나 파괴되어도 이탈을 계속하도록 목적지를 시작 시점에 고정한다.
            Vector3 destination = position + direction.normalized * _distance.Value;
            if (!Motion.BeginSmoothMovementLeg(destination))
            {
                return Status.Failure;
            }
            Monster.TrySetBehaviorState(MonsterState.Return);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || Motion.IsMovementLocked || !HasValidSettings())
            {
                return Status.Failure;
            }
            if (Motion.StepSmoothMovement(_speed.Value))
            {
                return Status.Success;
            }
            _elapsedTime += Time.deltaTime;
            return _elapsedTime >= _timeout.Value ? Status.Failure : Status.Running;
        }

        private bool HasValidSettings()
        {
            return _distance != null && _speed != null && _timeout != null
                && IsPositive(_distance.Value) && IsPositive(_speed.Value) && IsPositive(_timeout.Value);
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
