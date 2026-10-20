using UnityEngine;

//재장전 패시브
public interface ICannonReloadModifier
{
    float GetReloadDurationMultiplier(BoatController Boat);
}

