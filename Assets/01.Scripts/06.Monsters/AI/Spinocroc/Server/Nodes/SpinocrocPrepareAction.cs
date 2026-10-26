using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [Serializable]
    [NodeDescription(name: "스피노크록 공격 준비", story: "[Self]가 잠수하고 재등장해 돌진 방향을 고정한다",
        category: "행동/등대/몬스터", id: "9b9bfe80bd734487af11d25323a15890")]
    public sealed class SpinocrocPrepareAction : MonsterAction
    {
        [NonSerialized] private SpinocrocPreparation _preparation;
        [NonSerialized] private bool _completed;

        protected override Status OnStart()
        {
            _completed = false;
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            _preparation = Self.Value.GetComponent<SpinocrocPreparation>();
            return _preparation != null && _preparation.IsPreparing ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _preparation == null || !_preparation.CanAct || !_preparation.IsPreparing)
            {
                return Status.Failure;
            }
            _completed = _preparation.IsReadyForCharge;
            return _completed ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            if (!_completed && _preparation != null)
            {
                _preparation.Cancel();
            }
        }
    }
}
