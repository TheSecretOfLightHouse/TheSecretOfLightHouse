using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [Condition(name: "배가 공격 사거리 안에 있음", story: "[Self]의 공격 사거리에 [TargetBoat]가 있다",
        category: "조건/등대/몬스터", id: "7cc9c4f7f7f245c4b8192e53d40f535c")]
    public sealed class BoatInAttackRangeCondition : MonsterCondition
    {
        [NonSerialized] private IMonsterAttackRange _attackRange;

        public override void OnStart()
        {
            base.OnStart();
            _attackRange = Self != null && Self.Value != null
                ? Self.Value.GetComponent<IMonsterAttackRange>() : null;
        }

        public override bool IsTrue()
        {
            return CanEvaluate && _attackRange != null && _attackRange.IsTargetInRange(TargetBoat.Value);
        }
    }
}
