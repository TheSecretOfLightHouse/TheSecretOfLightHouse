using System;
using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Server;
using Lighthouse.Interaction.Validator.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Router.Server
{
    // Server-only entry for every JH02 request: common checks once, then the owning feature's handler.
    // Order follows jihun-system-design §5.2; unaccepted requests keep their number, accepted ones always consume it.
    public static class InteractionRouter
    {
        private static readonly Dictionary<InteractionAction, IInteractionHandler> Handlers =
            new Dictionary<InteractionAction, IInteractionHandler>();
        private static readonly RequestLedger Ledger = new RequestLedger();

        // Server-only. Raised after every reply is decided, accepted or not. For logs and tests; do not change state here.
        public static event Action<uint, InteractionRequest, InteractionResult> RequestProcessed;

        public static bool RegisterHandler(InteractionAction action, IInteractionHandler handler)
        {
            if (action == InteractionAction.None || handler == null)
            {
                Debug.LogError($"[{nameof(InteractionRouter)}] {action} cannot take a handler.");
                return false;
            }

            if (!Handlers.TryAdd(action, handler))
            {
                Debug.LogError($"[{nameof(InteractionRouter)}] A handler for {action} is already registered.");
                return false;
            }

            return true;
        }

        public static void UnregisterHandler(InteractionAction action, IInteractionHandler handler)
        {
            if (Handlers.TryGetValue(action, out IInteractionHandler current) && current == handler)
            {
                Handlers.Remove(action);
            }
        }

        // Called when an actor's relay starts or stops on the server, so numbering restarts with each match.
        internal static void BeginActor(uint actorId)
        {
            Ledger.Begin(actorId);
        }

        internal static void EndActor(uint actorId)
        {
            Ledger.End(actorId);
        }

        internal static InteractionResult Process(NetworkConnectionToClient sender, Vector3 senderPosition, in InteractionRequest request)
        {
            InteractionResult result = Decide(sender, senderPosition, request, out uint actorId);
            RequestProcessed?.Invoke(actorId, request, result);
            return result;
        }

        private static InteractionResult Decide(NetworkConnectionToClient sender, Vector3 senderPosition,
            in InteractionRequest request, out uint actorId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            actorId = 0;

            if (!manager || !manager.Connections.TryGetActorId(sender, out actorId))
            {
                return new InteractionResult(request.Sequence, InteractionResultCode.NotParticipant, 0);
            }

            Ledger.TryGetNextSequence(actorId, out uint nextSequence);

            // Input opens only after everyone loaded (2026-10-13). Loading lets early loaders act; Ended runs until shutdown.
            if (manager.Phase != MatchPhase.InProgress)
            {
                return new InteractionResult(request.Sequence, InteractionResultCode.NotInProgress, nextSequence);
            }

            switch (Ledger.Check(actorId, request.Sequence, out InteractionResult replay))
            {
                case RequestLedger.Verdict.Replay:
                    return replay;
                case RequestLedger.Verdict.Gap:
                    return new InteractionResult(request.Sequence, InteractionResultCode.SequenceGap, nextSequence);
                case RequestLedger.Verdict.Stale:
                    return new InteractionResult(request.Sequence, InteractionResultCode.StaleSequence, nextSequence);
                case RequestLedger.Verdict.UnknownActor:
                    return new InteractionResult(request.Sequence, InteractionResultCode.NotParticipant, 0);
            }

            InteractionResultCode code = Execute(actorId, sender, senderPosition, request);
            InteractionResult result = new InteractionResult(request.Sequence, code, request.Sequence + 1);
            Ledger.Record(actorId, result);
            return result;
        }

        private static InteractionResultCode Execute(uint actorId, NetworkConnectionToClient sender, Vector3 senderPosition,
            in InteractionRequest request)
        {
            InteractionResultCode code = TargetResolver.TryResolve(request, out ResolvedTarget target);

            if (code != InteractionResultCode.Success)
            {
                return code;
            }

            // The sender position is the owner's report and lags by the interpolation delay; ServerRange carries the slack.
            float rangeSqr = target.ServerRange * target.ServerRange;

            if ((target.Position - senderPosition).sqrMagnitude > rangeSqr)
            {
                return InteractionResultCode.OutOfRange;
            }

            if (!Handlers.TryGetValue(request.Action, out IInteractionHandler handler))
            {
                return InteractionResultCode.NotHandled;
            }

            InteractionContext context = new InteractionContext(actorId, sender, request.Action, target, request.Arg0, request.Arg1);

            try
            {
                code = handler.Handle(context);
            }
            catch (Exception exception)
            {
                // The number is still consumed so a retry cannot run a half-applied handler twice.
                Debug.LogException(exception);
                return InteractionResultCode.Rejected;
            }

            return code == InteractionResultCode.None ? InteractionResultCode.Rejected : code;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Handlers.Clear();
            Ledger.Clear();
            RequestProcessed = null;
        }
    }
}
