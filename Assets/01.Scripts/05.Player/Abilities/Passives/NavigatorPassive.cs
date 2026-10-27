using UnityEngine;

public sealed class NavigatorPassive : MonoBehaviour,
    IPlayerPassive, IBoatSpeedModifier
{
    [SerializeField, Min(1f)]
    private float _speedMultiplier = 1f;

    private PlayerController _player;

    public void Attach(PlayerController player)
    {
        _player = player;
        Refresh();
    }

    public void Refresh()
    {
    }

    public void Detach()
    {
        _player = null;
    }

    public float GetBoatSpeedMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !_player || !boat)
        {
            return 1f;
        }

        if (!_player.isActiveAndEnabled || _player.IsDowned
            || !_player.IsOnBoat || _player.CurrentBoat != boat
            || boat.Owner != _player || !boat.IsOccupied || boat.IsWrecked)
        {
            return 1f;
        }

        return Mathf.Max(1f, _speedMultiplier);
    }
}