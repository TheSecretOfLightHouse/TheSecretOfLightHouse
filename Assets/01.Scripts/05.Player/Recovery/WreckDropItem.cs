using UnityEngine;

public readonly struct WreckDropItem
{
    public bool IsModule { get; }
    public int ItemId { get; }
    public int Amount { get; }
    public Vector3 Position { get; }
    public uint ModuleId { get; }

    public WreckDropItem(
        bool isModule,
        int itemId,
        int amount,
        Vector3 position,
        uint moduleId = 0)
    {
        IsModule = isModule;
        ItemId = itemId;
        Amount = amount;
        Position = position;
        ModuleId = moduleId;
    }
}