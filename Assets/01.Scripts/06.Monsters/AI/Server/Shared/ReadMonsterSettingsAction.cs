using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "몬스터 공용 설정 읽기", story: "[Self]의 순찰 대기와 공격 재사용 시간을 Blackboard에 저장한다",
        category: "행동/등대/몬스터", id: "87d6a3f3826f4471a22c1fe82ad2083b")]
    public sealed class ReadMonsterSettingsAction : MonsterAction
    {
        [SerializeReference] private BlackboardVariable<float> _patrolWaitMin = new BlackboardVariable<float>();
        [SerializeReference] private BlackboardVariable<float> _patrolWaitMax = new BlackboardVariable<float>();
        [SerializeReference] private BlackboardVariable<float> _cooldownSeconds = new BlackboardVariable<float>();

        public BlackboardVariable<float> PatrolWaitMin { get => _patrolWaitMin; set => _patrolWaitMin = value; }
        public BlackboardVariable<float> PatrolWaitMax { get => _patrolWaitMax; set => _patrolWaitMax = value; }
        public BlackboardVariable<float> CooldownSeconds { get => _cooldownSeconds; set => _cooldownSeconds = value; }

        protected override Status OnStart()
        {
            if (!ResolveReferences() || _patrolWaitMin == null || _patrolWaitMax == null
                || _cooldownSeconds == null || Monster.Profile == null)
            {
                return Status.Failure;
            }

            Vector2 waitRange = Motion.PatrolWaitRange;
            if (float.IsNaN(waitRange.x) || float.IsInfinity(waitRange.x)
                || float.IsNaN(waitRange.y) || float.IsInfinity(waitRange.y)
                || waitRange.x < 0f || waitRange.y < waitRange.x)
            {
                return Status.Failure;
            }

            _patrolWaitMin.Value = waitRange.x;
            _patrolWaitMax.Value = waitRange.y;
            _cooldownSeconds.Value = Monster.Profile.AttackCooldown;
            Monster.TrySetBehaviorState(MonsterState.Patrol);
            return Status.Success;
        }

        protected override void OnEnd() { }
    }
}
