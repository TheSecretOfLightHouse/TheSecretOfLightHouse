using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "가오리 공격 후 추적", story: "[Self]가 [TargetBoat]를 추적하며 재사용 시간을 기다린다",
        category: "행동/등대/몬스터", id: "a13f4422c6f148d0b4203aef17d121a1")]
    public sealed class MantaRecoveryAction : MonsterAction
    {
        [NonSerialized] private float _elapsed;
        [NonSerialized] private float _duration;

        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile == null)
            {
                return Status.Failure;
            }
            _elapsed = 0f;
            _duration = Monster.Profile.AttackCooldown;
            if (float.IsNaN(_duration) || float.IsInfinity(_duration) || _duration < 0f)
            {
                return Status.Failure;
            }
            Monster.TrySetBehaviorState(MonsterState.Chase);
            return _duration == 0f ? Status.Success : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct)
            {
                return Status.Failure;
            }
            _elapsed += UnityEngine.Time.deltaTime;
            if (TargetBoat.Value == null
                || !Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance))
            {
                return Status.Failure;
            }
            if (!Motion.BeginSmoothMovementLeg(TargetBoat.Value.transform.position))
            {
                return Status.Failure;
            }
            Motion.StepSmoothMovement(Monster.Profile.ChaseSpeed);
            return _elapsed >= _duration ? Status.Success : Status.Running;
        }
    }
}
