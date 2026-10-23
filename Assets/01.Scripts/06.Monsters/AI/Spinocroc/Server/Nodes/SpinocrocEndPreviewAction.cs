using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [Serializable]
    [NodeDescription(name: "스피노크록 돌진 시험 종료", story: "[Self]가 돌진 시험을 마치고 배회로 돌아간다",
        category: "행동/등대/몬스터", id: "a013c971659145c29b7a364653472ccc")]
    public sealed class SpinocrocEndPreviewAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            SpinocrocPreparation preparation = Self.Value.GetComponent<SpinocrocPreparation>();
            if (preparation == null || !preparation.HasCompletedCharge)
            {
                return Status.Failure;
            }
            // 감속·회복을 연결하기 전까지 돌진 완료 뒤의 시험 반복을 명시적으로 종료한다.
            preparation.Cancel();
            return Status.Success;
        }
    }
}
