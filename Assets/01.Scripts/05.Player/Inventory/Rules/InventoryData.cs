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
}
