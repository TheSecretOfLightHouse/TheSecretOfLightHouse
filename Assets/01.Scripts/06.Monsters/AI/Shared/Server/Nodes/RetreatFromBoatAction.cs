using System;
using Lighthouse.Monsters.AI.Net;
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
        [NonSerialized] private float _elapsedTime;
        [NonSerialized] private BlueSharkAnimation _animation;
        [NonSerialized] private TrackingAttack _trackingAttack;

        protected override Status OnStart()
        {
            _elapsedTime = 0f;
            _animation = null;
            _trackingAttack = null;
            if (!ResolveReferences() || Motion.IsMovementLocked)
            {
                return Status.Failure;
            }
            _trackingAttack = Self.Value.GetComponent<TrackingAttack>();
            if (!HasValidSettings())
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
            Vector3 destination = position + direction.normalized * _trackingAttack.RetreatDistance;
            if (!Motion.BeginSmoothMovementLeg(destination))
            {
                return Status.Failure;
            }
            Monster.TrySetBehaviorState(MonsterState.Return);
            _animation = Self.Value.GetComponent<BlueSharkAnimation>();
            if (_animation != null)
            {
                _animation.SetAnimationState(BlueSharkAnimation.Dash);
            }
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || Motion.IsMovementLocked || !HasValidSettings())
            {
                return Status.Failure;
            }
            if (Motion.StepSmoothMovement(_trackingAttack.RetreatSpeed))
            {
                return Status.Success;
            }
            _elapsedTime += Time.deltaTime;
            return _elapsedTime >= _trackingAttack.RetreatTimeout ? Status.Failure : Status.Running;
        }

        protected override void OnEnd()
        {
            if (_animation != null)
            {
                _animation.SetAnimationState(BlueSharkAnimation.Idle);
            }
            base.OnEnd();
        }

        private bool HasValidSettings()
        {
            return _trackingAttack != null && _trackingAttack.isActiveAndEnabled
                && _trackingAttack.HasValidRetreatSettings;
        }
    }
}
