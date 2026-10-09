using UnityEngine;

public struct MoveConfig
{
    public float SpeedMultiplier { get; set; }
    public float AccelerationMultiplier { get; set; }
    public bool InputLocked { get; set; }

    public static MoveConfig Normal => new MoveConfig
    {
        SpeedMultiplier = 1f,
        AccelerationMultiplier = 1f,
        InputLocked = false
    };

    public static MoveConfig Locked => new MoveConfig
    {
        SpeedMultiplier = 0f,
        AccelerationMultiplier = 1f,
        InputLocked = true
    };
    public static MoveConfig Combine(MoveConfig movement,MoveConfig modifier)
    {
        return new MoveConfig
        {
            SpeedMultiplier = movement.SpeedMultiplier * modifier.SpeedMultiplier,

            AccelerationMultiplier = movement.AccelerationMultiplier * modifier.AccelerationMultiplier,

            InputLocked = movement.InputLocked || modifier.InputLocked
        };
    }
}
