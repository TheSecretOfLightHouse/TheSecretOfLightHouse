using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class GatheringFever : NetworkBehaviour
{
    [SerializeField, Min(0.01f)]
    private float _duration = 10f;

    private PlayerController _player;

    [SyncVar]
    private double _endsAt;

    public bool IsActive =>
        isActiveAndEnabled && NetworkTime.time < _endsAt;

    public float RemainingSeconds => IsActive
        ? (float)(_endsAt - NetworkTime.time)
        : 0f;

    private void Awake()
    {
        syncMode = SyncMode.Owner;
        _player = GetComponent<PlayerController>();
    }

    [Server]
    public bool TryStart()
    {
        if (!isActiveAndEnabled || IsActive
            || !_player || !_player.isActiveAndEnabled
            || _player.IsDowned
            || _duration <= 0f
            || float.IsNaN(_duration)
            || float.IsInfinity(_duration))
        {
            return false;
        }

        _endsAt = NetworkTime.time + _duration;
        return true;
    }
}
