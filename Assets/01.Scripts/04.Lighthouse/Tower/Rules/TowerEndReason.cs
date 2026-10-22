namespace Lighthouse.Tower.Rules
{
    // Why the match ended. Sent to everyone at the end, so it must not tell more than the result itself.
    // 3 and 5 belonged to the crystal relight and the special ending, dropped on 2026-10-22; 3 now means the repair.
    public enum TowerEndReason : byte
    {
        None = 0,
        // Ritual ran its full time with enough citizens pointing at the mafia.
        RitualSucceeded = 1,
        RitualFailed = 2,
        // Summoned at 100% repair, or the carried-over repair reached 100% before the lighthouse fell.
        RepairCompleted = 3,
        LighthouseDestroyed = 4,
        Deadline = 6
    }
}
