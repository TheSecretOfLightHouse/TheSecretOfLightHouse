using System;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Network.Connections.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Relay.Net
{
    // Sends interaction requests from the owning player to the server. It lives on the player object because Mirror
    // only accepts a Command from the object's owner, which is what lets the server know who sent it.
    // One request at a time: the next waits for the reply, and nothing is resent (a match ends if the connection drops).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkActor))]
    public sealed class InteractionRelay : NetworkBehaviour
    {
        private NetworkActor _actor;
        private uint _nextSequence = 1;
        private uint _pendingSequence;
        private bool _isAwaitingResult;

        public bool IsAwaitingResult => _isAwaitingResult;

        // Client-side, owner only.
        public event Action<InteractionResult> ResultReceived;

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        public override void OnStartServer()
        {
            // The actor id is assigned before spawn, so it is valid here; each match spawns a new player and relay.
            InteractionRouter.BeginActor(_actor.ActorId);
        }

        public override void OnStopServer()
        {
            InteractionRouter.EndActor(_actor.ActorId);
        }

        [Client]
        public bool TrySendToNetworkTarget(uint targetNetId, InteractionAction action, int arg0 = 0, int arg1 = 0)
        {
            return TrySend(InteractionRequest.ForNetworkTarget(0, targetNetId, action, arg0, arg1));
        }

        [Client]
        public bool TrySendToNonNetworkTarget(InteractionTargetKind kind, int targetKey, InteractionAction action, int arg0 = 0, int arg1 = 0)
        {
            return TrySend(InteractionRequest.ForNonNetworkTarget(0, kind, targetKey, action, arg0, arg1));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test-only: sends a request as given, skipping the one-at-a-time guard and numbering, to check server-side rejection.
        [Client]
        internal void SendUncheckedForTest(InteractionRequest request)
        {
            if (isOwned)
            {
                CmdRequest(request);
            }
        }

        internal uint NextSequenceForTest => _nextSequence;
#endif

        private bool TrySend(InteractionRequest request)
        {
            if (!isOwned || _isAwaitingResult)
            {
                return false;
            }

            _pendingSequence = _nextSequence;
            _isAwaitingResult = true;
            CmdRequest(request.WithSequence(_pendingSequence));
            return true;
        }

        [Command]
        private void CmdRequest(InteractionRequest request)
        {
            InteractionResult result = InteractionRouter.Process(connectionToClient, transform.position, request);
            TargetReceiveResult(result);
        }

        [TargetRpc]
        private void TargetReceiveResult(InteractionResult result)
        {
            // Resync from the server: an unaccepted number is reused, an accepted one moves on.
            if (result.NextExpectedSequence != 0)
            {
                _nextSequence = result.NextExpectedSequence;
            }

            if (_isAwaitingResult && result.Sequence == _pendingSequence)
            {
                _isAwaitingResult = false;
            }

            ResultReceived?.Invoke(result);
        }
    }
}
