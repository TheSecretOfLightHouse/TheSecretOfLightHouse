using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Lighthouse.Monsters.AI.Server", "Lighthouse.Runtime", "ChaseBoatAction")]
    [Serializable]
    [NodeDescription(
        name: "돌진 공격이 가능할 때까지 추적",
        story: "[Self]가 [TargetBoat]를 공격 범위와 재사용 대기 조건이 충족될 때까지 추적한다",
        category: "행동/등대/몬스터",
        id: "8d3747fc1ce14644b834fe20f9a63b25")]
    public sealed class ChaseUntilChargeReadyAction : MonsterAction
    {
        [UnityEngine.SerializeReference] private BlackboardVariable<float> _cooldownRemaining = new BlackboardVariable<float>();
        [NonSerialized] private SeaMonsterChargeAttack _attack;

        public BlackboardVariable<float> CooldownRemaining { get => _cooldownRemaining; set => _cooldownRemaining = value; }

        protected override Status OnStart()
        {
            if (!ResolveReferences() || Monster.Profile.ChaseSpeed <= 0f)
            {
                return Status.Failure;
            }

            _attack = Self.Value.GetComponent<SeaMonsterChargeAttack>();
            if (_attack == null || !_attack.CanAct)
            {
                return Status.Failure;
            }

            Monster.TrySetBehaviorState(MonsterState.Chase);
            UpdateCooldown();
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
                return Status.Failure;
            }

            if (_attack == null || !_attack.CanAct)
            {
                return Status.Failure;
            }

            UpdateCooldown();

            if (_attack.CanStartAttack(TargetBoat.Value))
            {
                return Status.Success;
            }

            Motion.StepTowards(TargetBoat.Value.transform.position, Monster.Profile.ChaseSpeed,
                UnityEngine.Mathf.Min(Motion.ChaseStoppingDistance, _attack.AttackRange));
            return Status.Running;
        }

        private void UpdateCooldown()
        {
            if (_cooldownRemaining != null)
            {
                _cooldownRemaining.Value = _attack.CooldownRemaining;
            }
        }
    }
}
