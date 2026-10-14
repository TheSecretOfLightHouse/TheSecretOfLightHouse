using System;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Sessions.Net;
using Lighthouse.Interaction.Sessions.Rules;
using Lighthouse.Interaction.Sessions.Server;
using Lighthouse.Network.Connections.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Relay.Net
{
    // Sends interaction requests from the owning player to the server. It lives on the player object because Mirror
    // only accepts a Command from the object's owner, which is what lets the server know who sent it.
    // One request at a time: the next waits for the reply, and nothing is resent (a match ends if the connection drops).
    // On the server it also ticks this actor's held session and tells the owner when one starts or ends.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkActor))]
    public sealed class InteractionRelay : NetworkBehaviour
    {
        private NetworkActor _actor;
        private uint _nextSequence = 1;
        private uint _pendingSequence;
        private bool _isAwaitingResult;

        // Server-side: the last start and end the owner was told about, so each is sent exactly once.
        private uint _notifiedStartId;
        private uint _notifiedEndId;

        // Client-side, owner only.
        private InteractionSessionUpdate _session;

        public bool IsAwaitingResult => _isAwaitingResult;
        public bool HasActiveSession => _session.IsActive;
        public InteractionSessionUpdate Session => _session;

        // Client-side, owner only.
        public event Action<InteractionResult> ResultReceived;
        public event Action<InteractionSessionUpdate> SessionChanged;

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        private void Update()
        {
            if (isServer)
            {
                ServerTickSession();
            }
        }

        public override void OnStartServer()
        {
            // The actor id is assigned before spawn, so it is valid here; each match spawns a new player and relay.
            InteractionRouter.BeginActor(_actor.ActorId);
        }

        public override void OnStopServer()
        {
            // Leaving (or the match ending) must not leave a session behind holding its target.
            InteractionSessions.ForgetActor(_actor.ActorId);
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

        // Ends the owner's running session, e.g. when the hold key is released. Unnumbered: it only stops the
        // sender's own session, changes no resources, and repeating it does nothing.
        [Client]
        public bool TryCancelSession()
        {
            if (!isOwned || !_session.IsActive)
            {
                return false;
            }

            CmdCancelSession(_session.SessionId);
            return true;
        }

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

        [Command]
        private void CmdCancelSession(uint sessionId)
        {
            InteractionSessions.TryCancel(_actor.ActorId, sessionId, InteractionSessionEndReason.CancelledByPlayer);
        }

        [Server]
        private void ServerTickSession()
        {
            uint actorId = _actor.ActorId;
            InteractionSessions.Tick(actorId, transform.position);

            // End first: a session may start and end between two ticks, and a new one may start right after.
            if (InteractionSessions.TryGetLastEnded(actorId, out InteractionSessions.EndRecord ended)
                && ended.SessionId != _notifiedEndId)
            {
                _notifiedEndId = ended.SessionId;
                TargetSessionChanged(new InteractionSessionUpdate(ended.SessionId, InteractionAction.None, false, 0f, ended.Reason));
            }

            if (InteractionSessions.TryGetActive(actorId, out InteractionSession session) && session.SessionId != _notifiedStartId)
            {
                _notifiedStartId = session.SessionId;
                TargetSessionChanged(new InteractionSessionUpdate(session.SessionId, session.Context.Action, true,
                    session.DurationSeconds, InteractionSessionEndReason.None));
            }
        }

        [TargetRpc]
        private void TargetSessionChanged(InteractionSessionUpdate update)
        {
            // An end notice only clears the session it belongs to.
            if (update.IsActive || update.SessionId == _session.SessionId)
            {
                _session = update;
            }

            SessionChanged?.Invoke(update);
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
