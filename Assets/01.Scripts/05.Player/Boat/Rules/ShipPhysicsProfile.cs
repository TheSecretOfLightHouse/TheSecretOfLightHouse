using UnityEngine;

namespace Lighthouse.Map.Net.Contracts
{
    public struct ShipPhysicsProfile
    {
        public float Mass;
        public Vector3 CenterOfMassOffset;
        public float SailArea;
        public float RightingStrength;
    }
}
