using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "배를 발견할 때까지 순찰", story: "[Self]가 [TargetBoat]를 발견할 때까지 순찰한다",
        category: "행동/등대/몬스터", id: "6db73531b36140d194b3326bd0b011a1")]
    public sealed class PatrolUntilBoatDetectedAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile.PatrolSpeed <= 0f)
            {
                return Status.Failure;
            }

            TargetBoat.Value = null;
            Monster.TrySetBehaviorState(MonsterState.Patrol);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct)
            {
                return Status.Failure;
            }

            if (Sensor.TryAcquireTarget(Monster.Profile.DetectionRadius, out GameObject target))
            {
                TargetBoat.Value = target;
                return Status.Success;
            }

            return Motion.StepPatrol(Monster.Profile.PatrolSpeed) ? Status.Running : Status.Failure;
        }
    }
}
