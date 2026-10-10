namespace Lighthouse.Map.Net.Contracts
{
    public readonly struct InteractionRequest
    {
        public readonly uint ActorId;
        public readonly uint TargetId;
        public readonly InteractionAction Action;
        public readonly int Arg0;
        public readonly int Arg1;
        public readonly double ServerTime;

        public InteractionRequest(uint actorId, uint targetId, InteractionAction action, int arg0, int arg1, double serverTime)
        {
            ActorId = actorId;
            TargetId = targetId;
            Action = action;
            Arg0 = arg0;
            Arg1 = arg1;
            ServerTime = serverTime;
        }
    }
}
