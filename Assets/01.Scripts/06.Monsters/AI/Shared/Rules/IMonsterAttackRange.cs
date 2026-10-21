using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    public interface IMonsterAttackRange
    {
        bool CanAct { get; }
        float AttackRange { get; }
        bool IsTargetInRange(GameObject target);
    }
}
