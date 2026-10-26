using UnityEngine;
using System.Collections.Generic;

public sealed class InventoryData
{
    public const int SlotCapacity = 8;
    public const int StackCapacity = 10;

    private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

    private int _stackLimit = StackCapacity;

    public int StackLimit => _stackLimit;

    public int UsedSlots
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<int, int> entry in _counts)
            {
                total += GetStackCount(entry.Value, _stackLimit);
            }
            return total;
        }
    }

    public static int GetStackCount(int count)
    {
        return GetStackCount(count, StackCapacity);
    }

    public static int GetStackCount(int count, int stackCapacity)
    {
        int capacity = System.Math.Max(1, stackCapacity);
        return count > 0 ? 1 + (count - 1) / capacity : 0;
    }

    public void SetStackCapacity(int capacity)
    {
        _stackLimit = System.Math.Max(StackCapacity, capacity);
    }

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
        if(id <= 0 || amount<=0)
        {
            return false;
        }

        int currentCount = GetCount(id);

        if(currentCount> int.MaxValue - amount)
        {
            return false;
        }
        int newCount = currentCount + amount;
        int newSlots = GetStackCount(newCount, _stackLimit) - GetStackCount(currentCount, _stackLimit);
        if (newSlots > SlotCapacity - UsedSlots)
        {
            return false;
        }
        _counts[id] = newCount;
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
