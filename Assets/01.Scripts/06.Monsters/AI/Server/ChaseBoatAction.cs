using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "Chase Boat Until Lost", story: "[Self]가 [TargetBoat]이 활동 범위를 벗어날 때까지 추격",
        category: "Action/Lighthouse/Monsters", id: "8d3747fc1ce14644b834fe20f9a63b25")]
    public sealed class ChaseBoatAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile.ChaseSpeed <= 0f)
            {
                return Status.Failure;
            }

            Monster.TrySetBehaviorState(MonsterState.Chase);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct)
            {
                return Status.Failure;
            }

            if (!Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance))
            {
                return Status.Success;
            }

            Motion.StepTowards(TargetBoat.Value.transform.position, Monster.Profile.ChaseSpeed,
                Motion.ChaseStoppingDistance);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            if (TargetBoat != null)
            {
                TargetBoat.Value = null;
            }
        }
    }
}
