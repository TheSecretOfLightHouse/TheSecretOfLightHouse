namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    public readonly struct CitizenAbyssSnapshot
    {
        public uint ActorId { get; }
        public uint TargetId { get; }
        public double SettledSeal { get; }
        public double SettledWrong { get; }
        public double PendingSealSeconds { get; }
        public double PendingWrongSeconds { get; }

        public CitizenAbyssSnapshot(uint actorId, uint targetId, double settledSeal, double settledWrong,
            double pendingSealSeconds, double pendingWrongSeconds)
        {
            ActorId = actorId;
            TargetId = targetId;
            SettledSeal = settledSeal;
            SettledWrong = settledWrong;
            PendingSealSeconds = pendingSealSeconds;
            PendingWrongSeconds = pendingWrongSeconds;
        }
    }
}
