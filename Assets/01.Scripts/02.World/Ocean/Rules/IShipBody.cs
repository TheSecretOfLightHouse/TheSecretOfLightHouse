using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IShipBody
    {
        uint ShipId { get; }
        Rigidbody Body { get; }
        IReadOnlyList<Vector3> BuoyancyPoints { get; }
        float Mass { get; }
        Vector3 CenterOfMassOffset { get; }
        float HullExposure { get; }
        float RightingStrength { get; }
    }
}
