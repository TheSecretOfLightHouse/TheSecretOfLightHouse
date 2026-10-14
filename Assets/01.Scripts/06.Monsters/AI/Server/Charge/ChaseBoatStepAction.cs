using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "배를 향해 한 프레임 이동", story: "[Self]가 [TargetBoat]를 향해 이동한다",
        category: "행동/등대/몬스터", id: "2eca3c9487cf42e6a0cf5306e439e1df")]
    public sealed class ChaseBoatStepAction : MonsterAction
    {
        [NonSerialized] private float _lastStepTime;
        [NonSerialized] private bool _hasStepped;

        protected override Status OnStart()
        {
            if (!ResolveReferences() || !Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance)
                || Monster.Profile.ChaseSpeed <= 0f)
            {
                return Status.Failure;
            }
            if (Attack == null || !Attack.CanAct)
            {
                return Status.Failure;
            }
            // 선택 노드의 분기 실행에 여러 틱이 걸릴 수 있으므로 경과 시간을 이동량에 반영한다.
            float deltaTime = _hasStepped && Monster.State == MonsterState.Chase
                ? UnityEngine.Time.time - _lastStepTime : 0f;
            _lastStepTime = UnityEngine.Time.time;
            _hasStepped = true;
            Monster.TrySetBehaviorState(MonsterState.Chase);
            Motion.StepTowards(TargetBoat.Value.transform.position, Monster.Profile.ChaseSpeed,
                UnityEngine.Mathf.Min(Motion.ChaseStoppingDistance, Attack.AttackRange), deltaTime);
            return Status.Success;
        }
    }
}
