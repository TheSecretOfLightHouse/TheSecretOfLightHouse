using UnityEngine;

public sealed class RepairPassive : MonoBehaviour, IPlayerPassive, IBoatRepairModifier
{
    [SerializeField, Min(1f)]
    private float _repairAmountMultiplier = 1.5f;

    private PlayerController _player;
    private BoatController _currentBoat;

    public void Attach(PlayerController player)
    {
        Detach();

        _player = player;
        Refresh();
    }

    public void Refresh()
    {
        _currentBoat = null;

        if (!_player || !_player.isActiveAndEnabled || _player.IsDowned)
        {
            return;
        }

        BoatController boat = _player.CurrentBoat;

        if (!boat || !boat.IsOccupied || boat.IsWrecked || boat.Owner != _player)
        {
            return;
        }

        _currentBoat = boat;
    }

    public void Detach()
    {
        _currentBoat = null;
        _player = null;
    }

    public float GetRepairAmountMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !boat)
        {
            return 1f;
        }

        Refresh();

        if (_currentBoat != boat || float.IsNaN(_repairAmountMultiplier) || float.IsInfinity(_repairAmountMultiplier))
        {
            return 1f;
        }

        return Mathf.Max(1f, _repairAmountMultiplier);
    }
}