using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "랜덤 순찰 지점으로 이동", story: "[Self]가 해역 안 랜덤 지점으로 수영한다",
        category: "행동/등대/몬스터", id: "6aebd8ecbb0c4e309982140006430b40")]
    public sealed class PatrolToPointAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile.PatrolSpeed <= 0f || !Motion.BeginPatrolLeg())
            {
                return Status.Failure;
            }
            Monster.TrySetBehaviorState(MonsterState.Patrol);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct)
            {
                return Status.Failure;
            }
            return Motion.StepPatrol(Monster.Profile.PatrolSpeed) ? Status.Success : Status.Running;
        }
    }
}
