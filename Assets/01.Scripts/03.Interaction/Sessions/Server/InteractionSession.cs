using Lighthouse.Interaction.Router.Server;

namespace Lighthouse.Interaction.Sessions.Server
{
    // One running held interaction. An actor has at most one at a time.
    public readonly struct InteractionSession
    {
        public readonly uint SessionId;
        public readonly InteractionContext Context;
        public readonly double StartedAt;
        public readonly float DurationSeconds;
        public readonly IInteractionSessionHandler Handler;

        public InteractionSession(uint sessionId, InteractionContext context, double startedAt, float durationSeconds,
            IInteractionSessionHandler handler)
        {
            SessionId = sessionId;
            Context = context;
            StartedAt = startedAt;
            DurationSeconds = durationSeconds;
            Handler = handler;
        }

        public uint ActorId => Context.ActorId;
        public double EndsAt => StartedAt + DurationSeconds;
    }
}
