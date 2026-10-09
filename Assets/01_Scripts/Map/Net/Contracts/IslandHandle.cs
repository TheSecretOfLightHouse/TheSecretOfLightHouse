using UnityEngine;

namespace Lighthouse.Map.Net.Contracts
{
    public readonly struct IslandHandle
    {
        public readonly short Index;
        public readonly Vector3 Position;
        public readonly float Radius;

        public IslandHandle(short index, Vector3 position, float radius)
        {
            Index = index;
            Position = position;
            Radius = radius;
        }
    }
}
