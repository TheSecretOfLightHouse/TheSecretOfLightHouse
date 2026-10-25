using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    public class ExternalForceApplier
    {
        public void Apply(IShipBody ship, double time)
        {
            if (!WorldServices.TryGet(out IForceFieldProvider provider))
            {
                return;
            }

            Rigidbody body = ship.Body;
            Vector3 force = provider.AccumulateForce(ForceFieldRoute.Impulse, body.worldCenterOfMass, body.linearVelocity, ship.SailArea, time);

            body.AddForce(force);
        }
    }
}
