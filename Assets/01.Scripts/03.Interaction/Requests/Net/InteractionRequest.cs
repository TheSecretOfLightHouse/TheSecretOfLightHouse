using Lighthouse.Interaction.Requests.Rules;

namespace Lighthouse.Interaction.Requests.Net
{
    // Wire payload of one interaction request (Relay Command argument).
    // The sender's ActorId is not part of it: the server derives it from the connection the Command arrived on.
    public readonly struct InteractionRequest
    {
        public readonly uint Sequence;
        public readonly InteractionTargetKind TargetKind;
        // Network object targets: the target's netId. Not reused within a match because a match is one server run.
        public readonly uint TargetNetId;
        // Non-network targets: the owning feature's own id (e.g. SpotId), resolved by that feature's adapter on the server.
        public readonly int TargetKey;
        public readonly InteractionAction Action;
        public readonly int Arg0;
        public readonly int Arg1;

        public InteractionRequest(uint sequence, InteractionTargetKind targetKind, uint targetNetId, int targetKey,
            InteractionAction action, int arg0, int arg1)
        {
            Sequence = sequence;
            TargetKind = targetKind;
            TargetNetId = targetNetId;
            TargetKey = targetKey;
            Action = action;
            Arg0 = arg0;
            Arg1 = arg1;
        }

        public static InteractionRequest ForNetworkTarget(uint sequence, uint targetNetId, InteractionAction action, int arg0, int arg1)
        {
            return new InteractionRequest(sequence, InteractionTargetKind.NetworkObject, targetNetId, 0, action, arg0, arg1);
        }

        public static InteractionRequest ForNonNetworkTarget(uint sequence, InteractionTargetKind targetKind, int targetKey,
            InteractionAction action, int arg0, int arg1)
        {
            return new InteractionRequest(sequence, targetKind, 0, targetKey, action, arg0, arg1);
        }

        public InteractionRequest WithSequence(uint sequence)
        {
            return new InteractionRequest(sequence, TargetKind, TargetNetId, TargetKey, Action, Arg0, Arg1);
        }
    }
}
