using UnityEngine;

public sealed class DailyAbilityUsage
{
    private int _lastUsedDay;
    private bool _isApplying;

    public bool CanUse(int currentDay)
    {
        return !_isApplying && currentDay >= 1
            && currentDay > _lastUsedDay;
    }

    public bool TryConsume(int currentDay)
    {
        if (!CanUse(currentDay))
        {
            return false;
        }

        _lastUsedDay = currentDay;
        return true;
    }

    public bool TryExecute(int currentDay, System.Func<bool> tryApply)
    {
        if (!CanUse(currentDay) || tryApply == null)
        {
            return false;
        }

        // Prevent nested execution from spending the same daily allowance twice.
        _isApplying = true;
        try
        {
            if (!tryApply())
            {
                return false;
            }

            _lastUsedDay = currentDay;
            return true;
        }
        finally
        {
            _isApplying = false;
        }
    }
}
