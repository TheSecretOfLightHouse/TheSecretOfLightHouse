using System;
using UnityEngine;

public sealed class PlayerAnimationSource : IPlayerAnimationSource, IDisposable
{
    private readonly IPlayerStateSource _states;
    private readonly IPlayerAbilitySource _abilities;

    private bool _isJumping;

    public PlayerAnimationState Current {  get; private set; }
    public event Action<PlayerAnimationState> Changed;

    public PlayerAnimationSource(IPlayerStateSource states, IPlayerAbilitySource abilities)
    {
        _states = states;
        _abilities = abilities;

        _states.MotionChanged += HandleMotionChanged;
        _abilities.AbilityStarted += HandleAbilityStarted;
        _abilities.AbilityEnded += HandleAbilityEnded;

        Current = Resolve();
    }

    public void SetJumping(bool value)
    {
        if (_isJumping == value)
        {
            return;
        }

        _isJumping = value;
        Refresh();
    }

    private void HandleMotionChanged(PlayerMotionState state)
    {
        Refresh();
    }

    private void HandleAbilityStarted(IPlayerAbility ability)
    {
        Refresh();
    }

    private void HandleAbilityEnded(IPlayerAbility ability,bool cancelled)
    {
        Refresh();
    } 

    private void Refresh()
    {
        PlayerAnimationState next = Resolve();

        if (Current == next)
            return;

        Current = next;
        Changed?.Invoke(next);
    }

    private PlayerAnimationState Resolve()
    {
        if (_states.CurrentMotion == PlayerMotionState.Downed)
        {
            return PlayerAnimationState.Downed;
        }

        if (_states.CurrentMotion == PlayerMotionState.OnBoat)
        {
            return PlayerAnimationState.OnBoat;
        }

        return _states.CurrentMotion switch
        {
            PlayerMotionState.Jump => PlayerAnimationState.Jump,
            PlayerMotionState.Walk => PlayerAnimationState.Walk,
            PlayerMotionState.Run => PlayerAnimationState.Run,
            _ => PlayerAnimationState.Idle
        };
    }

    public void Dispose()
    {
        _states.MotionChanged -= HandleMotionChanged;
        _abilities.AbilityStarted -= HandleAbilityStarted;
        _abilities.AbilityEnded -= HandleAbilityEnded;

        Changed = null;
    }
}
