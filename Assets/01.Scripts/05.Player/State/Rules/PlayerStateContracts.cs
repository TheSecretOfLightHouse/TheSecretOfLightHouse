using System;
using UnityEngine;

public enum PlayerMotionState
{
    Idle,
    Walk,
    Run,
    OnBoat,
    Downed
}

public interface IPlayerStateSource
{
    PlayerMotionState CurrentMotion {  get; }
    event Action<PlayerMotionState> MotionChanged;
}