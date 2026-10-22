using UnityEngine;

public readonly struct ResourceDropHandle
{
    internal ResourcePickup Pickup { get; }
    internal ulong Generation { get; }

    internal ResourceDropHandle(ResourcePickup pickup, ulong generation)
    {
        Pickup = pickup;
        Generation = generation;
    }
}