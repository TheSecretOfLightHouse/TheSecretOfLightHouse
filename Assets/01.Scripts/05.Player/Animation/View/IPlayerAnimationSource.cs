using System;
using UnityEngine;

public enum PlayerAnimationState
{
    Idle,
    Walk,
    Run,
    OnBoat,
    Downed
}

public interface IPlayerAnimationSource
{
    PlayerAnimationState Current { get; }
    event Action<PlayerAnimationState> Changed;
}