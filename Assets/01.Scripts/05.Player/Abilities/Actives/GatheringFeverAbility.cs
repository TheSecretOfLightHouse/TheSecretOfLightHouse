using UnityEngine;
using System;

[DisallowMultipleComponent]
[RequireComponent(typeof(GatheringFever))]
public sealed class GatheringFeverAbility : PlayerAbility
{
    private GatheringFever _fever;
    private PlayerController _player;
    private PlayerMotor _motor;
    private bool _isFinished = true;

    public override bool IsFinished => _isFinished;

    public bool IsOnLand => _player && !_player.IsDowned
        && !_player.IsOnBoat && _motor && !_motor.IsOffGround;

    public event Action ActivationRequested;

    private void Awake()
    {
        _fever = GetComponent<GatheringFever>();
        _player = GetComponent<PlayerController>();
        _motor = GetComponent<PlayerMotor>();
    }

    public override bool CanExecute(IPlayerAbilityContext context)
    {
        return context != null
            && context.Actor == transform
            && !context.IsDowned
            && IsOnLand
            && _isFinished
            && _fever
            && _fever.isActiveAndEnabled
            && !_fever.IsActive;
    }

    public override void Begin(IPlayerAbilityContext context)
    {
        _isFinished = false;
        ActivationRequested?.Invoke();
    }

    public override void TickAbility(float deltaTime)
    {
    }

    public void CompleteRequest()
    {
        _isFinished = true;
    }

    public override void Cancel()
    {
        _isFinished = true;
    }
}
