using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    public class ShipStabilizer
    {
        private readonly BuoyancyTuning _tuning;

        public ShipStabilizer(BuoyancyTuning tuning)
        {
            _tuning = tuning;
        }

        public void Apply(IShipBody ship, float gravity, float submergedFraction)
        {
            if (submergedFraction <= 0f)
            {
                return;
            }

            Rigidbody body = ship.Body;
            Transform bodyTransform = body.transform;
            Vector3 torque = RightingModel.GetTorque(
                    bodyTransform.up,
                    bodyTransform.forward,
                    ship.RightingStrength,
                    ship.Mass,
                    gravity)
                + _tuning.GetAngularDragTorque(body.angularVelocity, ship.Mass);

            body.AddTorque(torque * submergedFraction);
        }
    }
}
