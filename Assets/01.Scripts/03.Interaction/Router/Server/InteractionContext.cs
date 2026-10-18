using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Server;
using Mirror;

namespace Lighthouse.Interaction.Router.Server
{
    // What a feature handler receives after the common checks passed: a known sender, an existing target in range,
    // and an action the target supports. Arg0~Arg2 are still unchecked; each handler validates its own meaning.
    public readonly struct InteractionContext
    {
        public readonly uint ActorId;
        public readonly NetworkConnectionToClient Sender;
        public readonly InteractionAction Action;
        public readonly ResolvedTarget Target;
        public readonly int Arg0;
        public readonly int Arg1;
        public readonly int Arg2;

        public InteractionContext(uint actorId, NetworkConnectionToClient sender, InteractionAction action,
            ResolvedTarget target, int arg0, int arg1, int arg2)
        {
            ActorId = actorId;
            Sender = sender;
            Action = action;
            Target = target;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
        }
    }
}
