using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [Serializable]
    [NodeDescription(name: "모사웨일 무피해 패턴 반복", story: "[Self]가 배회하고 모사웨일 준비 공격 회복 단계를 반복한다",
        category: "행동/등대/몬스터", id: "a2fa97d4f8f44dd2b4e5e74db867d17d")]
    public sealed class MosaWhaleCycleAction : MonsterAction
    {
        [NonSerialized] private MosaWhaleCycle _cycle;

        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            _cycle = Self.Value.GetComponent<MosaWhaleCycle>();
            return _cycle != null && _cycle.BeginCycle() ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            return CanAct && _cycle != null && _cycle.StepCycle() ? Status.Running : Status.Failure;
        }

        protected override void OnEnd()
        {
            if (_cycle != null)
            {
                _cycle.EndCycle();
            }
            base.OnEnd();
        }
    }
}
