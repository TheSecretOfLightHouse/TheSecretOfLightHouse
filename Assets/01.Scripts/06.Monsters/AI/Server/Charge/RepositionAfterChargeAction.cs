using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "돌진 후 가까운 임의 지점으로 이동",
        story: "[Self]가 [TargetBoat]의 추적 범위를 유지하며 가까운 임의 지점으로 이동한다",
        category: "행동/등대/몬스터", id: "a25fe7b640ac45e3bd7ad86da1526cb0")]
    public sealed class RepositionAfterChargeAction : MonsterAction
    {
        [NonSerialized] private float _elapsedTime;

        protected override Status OnStart()
        {
            _elapsedTime = 0f;
            if (!ResolveReferences() || !Motion.HasValidRepositionSettings || Motion.IsMovementLocked
                || float.IsNaN(Monster.Profile.PatrolSpeed) || float.IsInfinity(Monster.Profile.PatrolSpeed)
                || Monster.Profile.PatrolSpeed <= 0f)
            {
                return Status.Failure;
            }

            if (!Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance)
                || !Motion.BeginRepositionLeg(TargetBoat.Value.transform.position, Monster.Profile.MaxChaseDistance))
            {
                return Status.Success;
            }

            Monster.TrySetBehaviorState(MonsterState.Chase);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || Motion.IsMovementLocked)
            {
                return Status.Failure;
            }

            _elapsedTime += Time.deltaTime;
            // 배가 멀어지거나 장애물에 막혀도 이 보조 이동이 행동트리를 계속 점유하지 않도록 종료한다.
            if (_elapsedTime >= Motion.RepositionTimeout
                || !Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance)
                || !Motion.CanContinueReposition(TargetBoat.Value.transform.position, Monster.Profile.MaxChaseDistance))
            {
                return Status.Success;
            }

            return Motion.StepPatrol(Monster.Profile.PatrolSpeed) ? Status.Success : Status.Running;
        }
    }
}
