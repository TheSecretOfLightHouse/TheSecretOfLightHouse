using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "Patrol Until Boat Detected", story: "[Self] patrols until detecting [TargetBoat]",
        category: "Action/Lighthouse/Monsters", id: "6db73531b36140d194b3326bd0b011a1")]
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
