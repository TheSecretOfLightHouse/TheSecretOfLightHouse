using UnityEngine;

public sealed class GathererPassive : MonoBehaviour,
    IPlayerPassive, IInventoryStackModifier
{
    [SerializeField, Min(10)]
    private int _stackCapacity = 20;

    private PlayerController _player;

    public void Attach(PlayerController player)
    {
        _player = player;
    }

    public void Refresh()
    {
    }

    public void Detach()
    {
        _player = null;
    }

    public int GetStackCapacity()
    {
        if (!isActiveAndEnabled || !_player)
        {
            return InventoryData.StackCapacity;
        }

        return Mathf.Max(InventoryData.StackCapacity, _stackCapacity);
    }
}