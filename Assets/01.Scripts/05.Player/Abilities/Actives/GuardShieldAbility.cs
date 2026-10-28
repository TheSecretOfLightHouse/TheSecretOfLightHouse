using UnityEngine;
using System;

public sealed class GuardShieldAbility : PlayerAbility
{
    private bool _isFinished = true;

    public override bool IsFinished => _isFinished;

    public event Action SelectionRequested;

    public override bool CanExecute(IPlayerAbilityContext context)
    {
        return context is PlayerController player
            && IsAboardOwnedBoat(player)
            && _isFinished;
    }

    public static bool IsAboardOwnedBoat(PlayerController player)
    {
        if (!player || !player.isActiveAndEnabled || player.IsDowned || !player.IsOnBoat)
        {
            return false;
        }

        BoatController boat = player.CurrentBoat;
        return boat && boat.isActiveAndEnabled && boat == player.OwnedBoat
            && boat.Owner == player && boat.IsOccupied && !boat.IsWrecked;
    }

    public override void Begin(IPlayerAbilityContext context)
    {
        _isFinished = false;
        SelectionRequested?.Invoke();
    }

    public override void TickAbility(float deltaTime)
    {
    }

    public void CompleteSelection()
    {
        _isFinished = true;
    }

    public override void Cancel()
    {
        _isFinished = true;
    }
}
