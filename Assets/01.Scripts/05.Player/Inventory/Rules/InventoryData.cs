using UnityEngine;
using System.Collections.Generic;

public sealed class InventoryData
{
    private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

    public int GetCount(int id)
    {
        if (_counts.TryGetValue(id,out int count))
        {
            return count;
        }
        return 0;
    }

    public bool TryAdd(int id, int amount)
    {
        if(amount<=0)
        {
            return false;
        }

        int currentCount = GetCount(id);

        if(currentCount> int.MaxValue - amount)
        {
            return false;
        }
        _counts[id] = currentCount + amount;
        return true;
    }

    public int TryRemoveTo(int id, int requestedAmount)
    {
        if (requestedAmount <= 0)
        {
            return 0;
        }

        int currentCount = GetCount(id);
        int removedAmount = System.Math.Min(currentCount, requestedAmount);

        if (removedAmount <= 0)
        {
            return 0;
        }

        int remainingCount = currentCount - removedAmount;

        if (remainingCount == 0)
        {
            _counts.Remove(id);
        }
        else
        {
            _counts[id] = remainingCount;
        }

        return removedAmount;
    }

    public void CopyTo(List<KeyValuePair<int, int>> results)
    {
        results.Clear();

        foreach (KeyValuePair<int, int> entry in _counts)
        {
            results.Add(entry);
        }
    }
}
