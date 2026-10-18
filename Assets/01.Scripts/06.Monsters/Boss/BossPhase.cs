namespace Lighthouse.Monsters.Boss
{
    public enum BossPhase : byte
    {
        WaitingForSummon,
        NormalBattleReady,
        SpecialEndingReady,
        NormalBattleActive,
        LighthouseDestroyed,
        BattleStopped,
        Wrapping
    }
}
