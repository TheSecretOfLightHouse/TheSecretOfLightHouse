using Lighthouse.Interaction.Requests.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Targets.Server
{
    // A request target after the server confirmed it exists. Identity is set only for network object targets;
    // Key is set only for non-network targets.
    public readonly struct ResolvedTarget
    {
        public readonly InteractionTargetKind Kind;
        public readonly NetworkIdentity Identity;
        public readonly int Key;
        public readonly Vector3 Position;
        public readonly float ServerRange;

        public ResolvedTarget(InteractionTargetKind kind, NetworkIdentity identity, int key, Vector3 position, float serverRange)
        {
            Kind = kind;
            Identity = identity;
            Key = key;
            Position = position;
            ServerRange = serverRange;
        }
    }
}
