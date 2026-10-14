using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Targets.Server
{
    // Server-only. Turns the target named in a request into a confirmed target, or reports why it cannot.
    public static class TargetResolver
    {
        private static readonly Dictionary<InteractionTargetKind, INonNetworkTargetAdapter> Adapters =
            new Dictionary<InteractionTargetKind, INonNetworkTargetAdapter>();

        public static bool RegisterAdapter(InteractionTargetKind kind, INonNetworkTargetAdapter adapter)
        {
            if (kind == InteractionTargetKind.None || kind == InteractionTargetKind.NetworkObject || adapter == null)
            {
                Debug.LogError($"[{nameof(TargetResolver)}] {kind} cannot take an adapter.");
                return false;
            }

            if (!Adapters.TryAdd(kind, adapter))
            {
                Debug.LogError($"[{nameof(TargetResolver)}] An adapter for {kind} is already registered.");
                return false;
            }

            return true;
        }

        public static void UnregisterAdapter(InteractionTargetKind kind, INonNetworkTargetAdapter adapter)
        {
            if (Adapters.TryGetValue(kind, out INonNetworkTargetAdapter current) && current == adapter)
            {
                Adapters.Remove(kind);
            }
        }

        public static InteractionResultCode TryResolve(in InteractionRequest request, out ResolvedTarget target)
        {
            target = default;

            if (request.TargetKind == InteractionTargetKind.NetworkObject)
            {
                return TryResolveNetworkObject(request, out target);
            }

            if (!Adapters.TryGetValue(request.TargetKind, out INonNetworkTargetAdapter adapter)
                || !adapter.TryResolve(request.TargetKey, request.Action, out Vector3 position, out float serverRange))
            {
                return InteractionResultCode.InvalidTarget;
            }

            target = new ResolvedTarget(request.TargetKind, null, request.TargetKey, position, serverRange);
            return InteractionResultCode.Success;
        }

        private static InteractionResultCode TryResolveNetworkObject(in InteractionRequest request, out ResolvedTarget target)
        {
            target = default;

            // A despawned object is no longer in spawned, and its netId is never reused in the same match.
            if (!NetworkServer.spawned.TryGetValue(request.TargetNetId, out NetworkIdentity identity)
                || !identity
                || !identity.TryGetComponent(out InteractionTarget marker)
                || !marker.isActiveAndEnabled)
            {
                return InteractionResultCode.InvalidTarget;
            }

            if (!marker.Supports(request.Action))
            {
                return InteractionResultCode.ActionNotSupported;
            }

            target = new ResolvedTarget(InteractionTargetKind.NetworkObject, identity, 0, marker.Position, marker.ServerRange);
            return InteractionResultCode.Success;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Adapters.Clear();
        }
    }
}
