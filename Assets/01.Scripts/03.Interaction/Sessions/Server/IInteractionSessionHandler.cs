using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Sessions.Rules;

namespace Lighthouse.Interaction.Sessions.Server
{
    // Implemented by the feature that owns a held action (harvest MK03, rescue MK04...). The feature decides duration
    // and continue conditions (2026-10-13 decision); Interaction owns start, cancel, completion and cleanup.
    // Every call runs on the server within one frame. Apply the outcome only in Complete; Cancel must leave state as before.
    public interface IInteractionSessionHandler
    {
        // True when only one actor at a time may hold a session on the same target.
        bool IsExclusive { get; }

        // Called after the common checks. Return Success and a positive duration to start the session.
        InteractionResultCode Begin(in InteractionContext context, out float durationSeconds);

        // Checked every server frame while the session runs, after the common checks (phase, target, range).
        bool CanContinue(in InteractionSession session);

        void Complete(in InteractionSession session);

        void Cancel(in InteractionSession session, InteractionSessionEndReason reason);
    }
}
