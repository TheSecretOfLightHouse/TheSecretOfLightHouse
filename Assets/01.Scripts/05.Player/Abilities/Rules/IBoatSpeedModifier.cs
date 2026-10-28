using UnityEngine;

public interface IBoatSpeedModifier
{
    float GetBoatSpeedMultiplier(BoatController boat);
}
