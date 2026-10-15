using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Server;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Server
{
    public sealed class HarvestTargetAdapter : INonNetworkTargetAdapter
    {
        private readonly IHarvestSpotQuery _query;
        private readonly float _serverRange;

        public HarvestTargetAdapter(IHarvestSpotQuery query, float serverRange)
        {
            _query = query;
            _serverRange = serverRange;
        }

        public bool TryResolve(int key, InteractionAction action, out Vector3 position, out float serverRange)
        {
            position = default;
            serverRange = _serverRange;

            if (action != InteractionAction.Harvest || !_query.TryGetSpot(key, out SpotInfo info))
            {
                return false;
            }

            position = info.Position;
            return true;
        }
    }
}
