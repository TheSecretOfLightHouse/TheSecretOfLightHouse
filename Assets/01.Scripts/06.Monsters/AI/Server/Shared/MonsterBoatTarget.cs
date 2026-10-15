using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    public abstract class MonsterBoatTarget : MonoBehaviour
    {
        public abstract bool IsEligible { get; }
        public abstract float HitRadius { get; }

        public abstract bool TryApplyDamage(float damage);
    }
}
