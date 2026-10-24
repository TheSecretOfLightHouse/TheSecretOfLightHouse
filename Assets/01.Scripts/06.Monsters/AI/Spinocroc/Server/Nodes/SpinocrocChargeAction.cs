using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [Serializable]
    [NodeDescription(name: "스피노크록 고정 방향 돌진", story: "[Self]가 고정한 방향으로 돌진하며 공격한다",
        category: "행동/등대/몬스터", id: "37ea12c39e2942439f20251d8a226e50")]
    public sealed class SpinocrocChargeAction : MonsterAction
    {
        [NonSerialized] private SpinocrocCharge _charge;
        [NonSerialized] private SpinocrocPreparation _preparation;
        [NonSerialized] private bool _completed;

        protected override Status OnStart()
        {
            _completed = false;
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            _charge = Self.Value.GetComponent<SpinocrocCharge>();
            _preparation = Self.Value.GetComponent<SpinocrocPreparation>();
            return _charge != null && _charge.TryBegin() ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _charge == null)
            {
                return Status.Failure;
            }
            _completed = _charge.HasCompleted;
            return _completed ? Status.Success : (_charge.IsCharging ? Status.Running : Status.Failure);
        }

        protected override void OnEnd()
        {
            if (!_completed)
            {
                if (_charge != null)
                {
                    _charge.Cancel();
                }
                if (_preparation != null)
                {
                    _preparation.Cancel();
                }
            }
            base.OnEnd();
        }
    }
}
