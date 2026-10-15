using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [Condition(name: "배가 돌진 준비 범위 안에 있음", story: "[Self]의 돌진 준비 범위에 [TargetBoat]가 있다",
        category: "조건/등대/몬스터", id: "7cc9c4f7f7f245c4b8192e53d40f535c")]
    public sealed class BoatInAttackRangeCondition : MonsterCondition
    {
        public override bool IsTrue()
        {
            return CanEvaluate && Attack != null && Attack.IsTargetInRange(TargetBoat.Value);
        }
    }
}
