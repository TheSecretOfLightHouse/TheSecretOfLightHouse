using System;
using Unity.VisualScripting;
using UnityEngine;

public sealed class PlayerAnimationSource : IPlayerAnimationSource, IDisposable
{
    private readonly IPlayerStateSource _states;
    private readonly IPlayerAbilitySource _abilities;

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
            return PlayerAnimationState.Downed;

        IPlayerAbility ability = _abilities.CurrentAbility;

        if (ability != null)
        {
            //switch (ability.Kind)
            //{
            //    case PlayerAbilityKind.Hammer:
            //        return PlayerAnimationState.Hammer;

            //    case PlayerAbilityKind.Gather:
            //        return PlayerAnimationState.Gather;

            //    case PlayerAbilityKind.Repair:
            //        return PlayerAnimationState.Repair;
            //}
        }

        return _states.CurrentMotion switch
        {
            PlayerMotionState.Walk => PlayerAnimationState.Walk,

            PlayerMotionState.Run => PlayerAnimationState.Run,

            PlayerMotionState.OnBoat => PlayerAnimationState.OnBoat,

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
