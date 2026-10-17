using System.Collections.Generic;
using UnityEngine;

public sealed class PersonalHarvestData
{
    private readonly Dictionary<int, int> _remainingCounts = new Dictionary<int, int>();

    public int GetRemaining(int spotId, int initialCount)
    {
        if (spotId <= 0 || initialCount <= 0)
        {
            return 0;
        }

        return _remainingCounts.TryGetValue(spotId, out int remaining) ? remaining : initialCount;
    }

    public bool TryConsumeOne(int spotId, int initialCount, out int remaining)
    {
        remaining = GetRemaining(spotId, initialCount);

        if (remaining <= 0)
        {
            return false;
        }

        remaining--;
        _remainingCounts[spotId] = remaining;
        return true;
    }

    public void RestoreOne(int spotId, int initialCount)
    {
        if (_remainingCounts.TryGetValue(spotId, out int remaining) && remaining < initialCount)
        {
            _remainingCounts[spotId] = remaining + 1;
        }
    }
}
