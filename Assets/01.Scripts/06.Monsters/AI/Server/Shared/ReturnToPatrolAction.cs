using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(
        name: "순찰 시작 위치로 복귀",
        story: "[Self]가 [TargetBoat]를 정리하고 순찰 시작 위치로 돌아간다",
        category: "행동/등대/몬스터",
        id: "e2e306d7b4824859a83fe5dba8c9d4ea")]
    public sealed class ReturnToPatrolAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile.PatrolSpeed <= 0f)
            {
                return Status.Failure;
            }

            TargetBoat.Value = null;
            Monster.TrySetBehaviorState(MonsterState.Return);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct)
            {
                return Status.Failure;
            }

            return Motion.StepReturn(Monster.Profile.PatrolSpeed) ? Status.Success : Status.Running;
        }
    }
}
