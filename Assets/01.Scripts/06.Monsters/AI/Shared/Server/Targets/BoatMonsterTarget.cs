using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatController), typeof(BoatDurability))]
    public sealed class BoatMonsterTarget : MonsterBoatTarget
    {
        [SerializeField, Min(0.01f)]
        private float _hitRadius = 0.75f;

        private BoatController _boat;
        private BoatDurability _durability;

        public override bool IsEligible => isServer && isActiveAndEnabled
            && _boat && _boat.isActiveAndEnabled && _durability && _durability.isActiveAndEnabled
            && !_boat.IsWrecked && _boat.IsOccupied && MonsterMotion.IsFinite(transform.position);
        public override float HitRadius => _hitRadius;

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _durability = GetComponent<BoatDurability>();
        }

        [Server]
        public override bool TryApplyDamage(float damage)
        {
            if (!IsEligible || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
            {
                return false;
            }

            float previousDurability = _durability.CurrentDurability;
            _durability.ApplyDamage(damage, BoatDamageType.Monster);
            return _durability.CurrentDurability < previousDurability;
        }
    }
}
