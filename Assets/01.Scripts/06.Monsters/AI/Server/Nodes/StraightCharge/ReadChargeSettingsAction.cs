using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "돌진 몬스터 설정 읽기", story: "[Self]의 순찰 대기와 공격 시간을 Blackboard에 저장한다",
        category: "행동/등대/몬스터", id: "10c12f9b2baf44b4842b241675f5c382")]
    public sealed class ReadChargeSettingsAction : MonsterAction
    {
        [SerializeReference] private BlackboardVariable<float> _patrolWaitMin = new BlackboardVariable<float>();
        [SerializeReference] private BlackboardVariable<float> _patrolWaitMax = new BlackboardVariable<float>();
        [SerializeReference] private BlackboardVariable<float> _preparationSeconds = new BlackboardVariable<float>();
        [SerializeReference] private BlackboardVariable<float> _cooldownSeconds = new BlackboardVariable<float>();

        public BlackboardVariable<float> PatrolWaitMin { get => _patrolWaitMin; set => _patrolWaitMin = value; }
        public BlackboardVariable<float> PatrolWaitMax { get => _patrolWaitMax; set => _patrolWaitMax = value; }
        public BlackboardVariable<float> PreparationSeconds { get => _preparationSeconds; set => _preparationSeconds = value; }
        public BlackboardVariable<float> CooldownSeconds { get => _cooldownSeconds; set => _cooldownSeconds = value; }

        protected override Status OnStart()
        {
            if (!ResolveReferences() || _patrolWaitMin == null || _patrolWaitMax == null
                || _preparationSeconds == null || _cooldownSeconds == null)
            {
                return Status.Failure;
            }

            Vector2 waitRange = Motion.PatrolWaitRange;
            if (Attack == null || !Attack.CanAct || float.IsNaN(waitRange.x) || float.IsInfinity(waitRange.x)
                || float.IsNaN(waitRange.y) || float.IsInfinity(waitRange.y)
                || waitRange.x < 0f || waitRange.y < waitRange.x)
            {
                return Status.Failure;
            }

            _patrolWaitMin.Value = waitRange.x;
            _patrolWaitMax.Value = waitRange.y;
            _preparationSeconds.Value = Attack.PreparationDuration;
            _cooldownSeconds.Value = Monster.Profile.AttackCooldown;
            Monster.TrySetBehaviorState(MonsterState.Patrol);
            return Status.Success;
        }

        protected override void OnEnd() { }
    }
}
