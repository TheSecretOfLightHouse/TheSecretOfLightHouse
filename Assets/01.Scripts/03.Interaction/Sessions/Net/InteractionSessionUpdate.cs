using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Sessions.Rules;

namespace Lighthouse.Interaction.Sessions.Net
{
    // Personal notice to the session owner: sent once when a session starts and once when it ends.
    // Others see only what the owning feature makes public (e.g. a resource running out).
    public readonly struct InteractionSessionUpdate
    {
        public readonly uint SessionId;
        public readonly InteractionAction Action;
        public readonly bool IsActive;
        public readonly float DurationSeconds;
        public readonly InteractionSessionEndReason EndReason;

        public InteractionSessionUpdate(uint sessionId, InteractionAction action, bool isActive, float durationSeconds,
            InteractionSessionEndReason endReason)
        {
            SessionId = sessionId;
            Action = action;
            IsActive = isActive;
            DurationSeconds = durationSeconds;
            EndReason = endReason;
        }
    }
}
