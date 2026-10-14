using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(
        name: "고정 방향으로 직선 돌진",
        story: "[Self]가 확정한 마지막 배 위치를 향해 빠르게 직선 돌진한다",
        category: "행동/등대/몬스터",
        id: "ed18ab89f8bc4c49a56f8134761c4453")]
    public sealed class ChargeStraightAction : MonsterAction
    {
        [NonSerialized] private SeaMonsterChargeAttack _attack;

        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }

            _attack = Self.Value.GetComponent<SeaMonsterChargeAttack>();
            return _attack != null && _attack.BeginCharge() ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _attack == null || !_attack.CanAct)
            {
                return Status.Failure;
            }

            if (_attack.HasCompletedCharge)
            {
                return Status.Success;
            }

            return _attack.IsCharging || _attack.IsRecovering ? Status.Running : Status.Failure;
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            if (_attack != null)
            {
                _attack.Cancel();
            }
        }
    }
}
