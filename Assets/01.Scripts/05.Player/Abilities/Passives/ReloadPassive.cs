using UnityEngine;

public class ReloadPassive : MonoBehaviour, IPlayerPassive, ICannonReloadModifier
{

    [SerializeField, Range(0.01f, 1f)]
    private float _reloadDurationMultiplier = 0.5f;

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

        if (!boat || !boat.IsOccupied || boat.IsWrecked)
        {
            return;
        }

        if (boat.Owner != _player)
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

    public float GetReloadDurationMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !boat)
        {
            return 1f;
        }

        Refresh();

        if (_currentBoat != boat)
        {
            return 1f;
        }

        return Mathf.Clamp(_reloadDurationMultiplier, 0.01f, 1f);
    }
}