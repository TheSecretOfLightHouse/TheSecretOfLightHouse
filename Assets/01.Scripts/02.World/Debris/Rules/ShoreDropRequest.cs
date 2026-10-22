using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public readonly struct ShoreDropRequest
    {
        public readonly Vector3 Origin;
        public readonly DebrisKind Kind;
        public readonly int Slot;

        public ShoreDropRequest(Vector3 origin, DebrisKind kind, int slot)
        {
            Origin = origin;
            Kind = kind;
            Slot = slot;
        }
    }
}
