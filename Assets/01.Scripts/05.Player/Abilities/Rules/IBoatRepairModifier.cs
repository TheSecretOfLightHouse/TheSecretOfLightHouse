using UnityEngine;

public interface IBoatRepairModifier
{
    float GetRepairAmountMultiplier(BoatController boat);
}