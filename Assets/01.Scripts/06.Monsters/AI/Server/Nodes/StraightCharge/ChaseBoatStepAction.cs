using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "배를 향해 이동", story: "[Self]가 [TargetBoat]를 향해 이동한다",
        category: "행동/등대/몬스터", id: "2eca3c9487cf42e6a0cf5306e439e1df")]
    public sealed class ChaseBoatStepAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }

            return RequestChase();
        }

        protected override Status OnUpdate()
        {
            return CanAct ? RequestChase() : Status.Failure;
        }

        private Status RequestChase()
        {
            if (!Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance)
                || Monster.Profile.ChaseSpeed <= 0f || Attack == null || !Attack.CanAct)
            {
                return Status.Failure;
            }

            if (Attack.IsTargetInRange(TargetBoat.Value))
            {
                Motion.Stop();
                return Status.Success;
            }

            Monster.TrySetBehaviorState(MonsterState.Chase);
            // 물리 갱신 전에 종료 콜백이 이동 요청을 지우지 않도록 도착할 때까지 실행한다.
            return Motion.StepTowards(TargetBoat.Value.transform.position, Monster.Profile.ChaseSpeed,
                UnityEngine.Mathf.Min(Motion.ChaseStoppingDistance, Attack.AttackRange))
                ? Status.Success : Status.Running;
        }
    }
}
