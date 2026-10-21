namespace Lighthouse.Tower.Rules
{
    // Where the lighthouse is in the match. Final battle is entered only by a summon and has no way back.
    public enum TowerPhase : byte
    {
        Repairing = 0,
        Ritual = 1,
        FinalBattle = 2,
        Ended = 3
    }
}
