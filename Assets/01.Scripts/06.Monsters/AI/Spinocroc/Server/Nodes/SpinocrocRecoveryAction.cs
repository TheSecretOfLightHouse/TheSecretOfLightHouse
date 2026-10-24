using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [Serializable]
    [NodeDescription(name: "스피노크록 감속과 회복", story: "[Self]가 감속하고 반격에 노출된 뒤 배회로 돌아간다",
        category: "행동/등대/몬스터", id: "fc941a4978f149169617d9effe68428f")]
    public sealed class SpinocrocRecoveryAction : MonsterAction
    {
        [NonSerialized] private SpinocrocRecovery _recovery;
        [NonSerialized] private SpinocrocPreparation _preparation;

        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            _recovery = Self.Value.GetComponent<SpinocrocRecovery>();
            _preparation = Self.Value.GetComponent<SpinocrocPreparation>();
            return _recovery != null && (_recovery.IsRecovering || _recovery.HasCompleted)
                ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _recovery == null)
            {
                return Status.Failure;
            }
            return _recovery.HasCompleted ? Status.Success : (_recovery.IsRecovering ? Status.Running : Status.Failure);
        }

        protected override void OnEnd()
        {
            if (_recovery != null)
            {
                _recovery.Cancel();
            }
            if (_preparation != null)
            {
                _preparation.Cancel();
            }
            base.OnEnd();
        }
    }
}
