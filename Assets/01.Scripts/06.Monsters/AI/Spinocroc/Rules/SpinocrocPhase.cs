namespace Lighthouse.Monsters.Spinocroc.Rules
{
    public enum SpinocrocPhase : byte
    {
        Patrol,
        Diving,
        Reappearing,
        Windup,
        DirectionLocked,
        ReadyForCharge,
        Dead,
        Charging,
        ChargeCompleted,
        Decelerating,
        Recovering,
        RecoveryCompleted
    }
}
