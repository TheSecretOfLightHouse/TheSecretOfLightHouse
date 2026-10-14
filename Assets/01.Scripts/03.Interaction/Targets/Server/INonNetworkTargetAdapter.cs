using Lighthouse.Interaction.Requests.Net;
using UnityEngine;

namespace Lighthouse.Interaction.Targets.Server
{
    // Implemented by the feature that owns a non-network target kind (e.g. harvest spots) so the server can check
    // a client-sent key without Interaction knowing that feature's types.
    public interface INonNetworkTargetAdapter
    {
        // serverRange already includes the slack for the late reported player position.
        bool TryResolve(int key, InteractionAction action, out Vector3 position, out float serverRange);
    }
}
