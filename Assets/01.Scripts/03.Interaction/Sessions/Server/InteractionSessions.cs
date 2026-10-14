using System;
using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Sessions.Rules;
using Lighthouse.Interaction.Targets.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using UnityEngine;

namespace Lighthouse.Interaction.Sessions.Server
{
    // Server-only. Running held interactions, at most one per actor (jihun-system-design §6).
    // Each actor's relay ticks its own session every server frame, so no separate driver object is needed.
    public static class InteractionSessions
    {
        private static readonly Dictionary<uint, InteractionSession> Active = new Dictionary<uint, InteractionSession>();
        private static readonly Dictionary<uint, EndRecord> LastEnded = new Dictionary<uint, EndRecord>();

        private static uint _nextSessionId = 1;

        public readonly struct EndRecord
        {
            public readonly uint SessionId;
            public readonly InteractionSessionEndReason Reason;

            public EndRecord(uint sessionId, InteractionSessionEndReason reason)
            {
                SessionId = sessionId;
                Reason = reason;
            }
        }

        // Server-only. Raised once per session end. For logs and tests; do not change state here.
        public static event Action<InteractionSession, InteractionSessionEndReason> SessionEnded;

        public static bool TryGetActive(uint actorId, out InteractionSession session)
        {
            return Active.TryGetValue(actorId, out session);
        }

        public static bool TryGetLastEnded(uint actorId, out EndRecord record)
        {
            return LastEnded.TryGetValue(actorId, out record);
        }

        public static bool IsTargetTaken(in ResolvedTarget target, uint exceptActorId)
        {
            foreach (KeyValuePair<uint, InteractionSession> pair in Active)
            {
                if (pair.Key != exceptActorId && IsSameTarget(pair.Value.Context.Target, target))
                {
                    return true;
                }
            }

            return false;
        }

        internal static InteractionResultCode TryStart(in InteractionContext context, IInteractionSessionHandler handler)
        {
            if (Active.ContainsKey(context.ActorId)
                || (handler.IsExclusive && IsTargetTaken(context.Target, context.ActorId)))
            {
                return InteractionResultCode.Busy;
            }

            InteractionResultCode code = handler.Begin(context, out float durationSeconds);

            if (code != InteractionResultCode.Success)
            {
                return code == InteractionResultCode.None ? InteractionResultCode.Rejected : code;
            }

            if (durationSeconds <= 0f || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds))
            {
                // Begin succeeded but gave no usable duration: undo through Cancel so the feature keeps a single exit path.
                InteractionSession invalid = new InteractionSession(0, context, Time.timeAsDouble, 0f, handler);
                SafeCancel(invalid, InteractionSessionEndReason.HandlerError);
                Debug.LogError($"[{nameof(InteractionSessions)}] {context.Action} returned an invalid duration {durationSeconds}.");
                return InteractionResultCode.Rejected;
            }

            uint sessionId = _nextSessionId++;
            Active[context.ActorId] = new InteractionSession(sessionId, context, Time.timeAsDouble, durationSeconds, handler);
            return InteractionResultCode.Success;
        }

        // Called by the actor's relay every server frame.
        internal static void Tick(uint actorId, Vector3 actorPosition)
        {
            if (!Active.TryGetValue(actorId, out InteractionSession session))
            {
                return;
            }

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (!manager || manager.Phase != MatchPhase.InProgress)
            {
                End(session, InteractionSessionEndReason.MatchEnded);
                return;
            }

            if (!TargetResolver.TryRefresh(session.Context.Target, session.Context.Action, out Vector3 targetPosition))
            {
                End(session, InteractionSessionEndReason.TargetLost);
                return;
            }

            float range = session.Context.Target.ServerRange;

            if ((targetPosition - actorPosition).sqrMagnitude > range * range)
            {
                End(session, InteractionSessionEndReason.OutOfRange);
                return;
            }

            bool canContinue;

            try
            {
                canContinue = session.Handler.CanContinue(session);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                End(session, InteractionSessionEndReason.HandlerError);
                return;
            }

            if (!canContinue)
            {
                End(session, InteractionSessionEndReason.ConditionFailed);
                return;
            }

            if (Time.timeAsDouble >= session.EndsAt)
            {
                Finish(session);
            }
        }

        // Ends the actor's session if it is the given one. Idempotent: a stale or repeated cancel does nothing.
        internal static bool TryCancel(uint actorId, uint sessionId, InteractionSessionEndReason reason)
        {
            if (!Active.TryGetValue(actorId, out InteractionSession session) || session.SessionId != sessionId)
            {
                return false;
            }

            End(session, reason);
            return true;
        }

        internal static void CancelAny(uint actorId, InteractionSessionEndReason reason)
        {
            if (Active.TryGetValue(actorId, out InteractionSession session))
            {
                End(session, reason);
            }
        }

        internal static void ForgetActor(uint actorId)
        {
            CancelAny(actorId, InteractionSessionEndReason.ActorLeft);
            LastEnded.Remove(actorId);
        }

        private static void Finish(in InteractionSession session)
        {
            // Remove first so a handler that re-enters (e.g. despawns its target) cannot end the same session twice.
            Active.Remove(session.ActorId);

            try
            {
                session.Handler.Complete(session);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Record(session, InteractionSessionEndReason.HandlerError);
                return;
            }

            Record(session, InteractionSessionEndReason.Completed);
        }

        private static void End(in InteractionSession session, InteractionSessionEndReason reason)
        {
            Active.Remove(session.ActorId);
            SafeCancel(session, reason);
            Record(session, reason);
        }

        private static void SafeCancel(in InteractionSession session, InteractionSessionEndReason reason)
        {
            try
            {
                session.Handler.Cancel(session, reason);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Record(in InteractionSession session, InteractionSessionEndReason reason)
        {
            LastEnded[session.ActorId] = new EndRecord(session.SessionId, reason);
            SessionEnded?.Invoke(session, reason);
        }

        private static bool IsSameTarget(in ResolvedTarget a, in ResolvedTarget b)
        {
            if (a.Kind != b.Kind)
            {
                return false;
            }

            return a.Kind == InteractionTargetKind.NetworkObject ? a.Identity == b.Identity : a.Key == b.Key;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            LastEnded.Clear();
            _nextSessionId = 1;
            SessionEnded = null;
        }
    }
}
