using Mirror;
using UnityEngine;
using System;
using Lighthouse.Player.Boat.View;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatDurability))]
public sealed class BoatShield : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnShieldChanged))]
    private bool _hasShield;

    private BoatDurability _durability;
    private BoatShieldView _view;

    public bool HasShield => _hasShield;
    public event Action<bool> ShieldChanged;

    private void Awake()
    {
        _durability = GetComponent<BoatDurability>();
        _view = GetComponent<BoatShieldView>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _hasShield = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ShieldChanged?.Invoke(_hasShield);
    }

    public override void OnStopClient()
    {
        ShieldChanged?.Invoke(false);
        base.OnStopClient();
    }

    private void OnShieldChanged(bool oldValue, bool newValue)
    {
        ShieldChanged?.Invoke(newValue);
    }

    [Server]
    public void GrantShield()
    {
        TryGrantShield();
    }

    [Server]
    public bool TryGrantShield()
    {
        if (!isActiveAndEnabled || _hasShield || !_durability
            || !_durability.isActiveAndEnabled || _durability.IsWrecked)
        {
            return false;
        }

        _hasShield = true;
        RpcPlayShieldPulse(false);
        return true;
    }

    [Server]
    public bool TryAbsorbDamage()
    {
        if (!_hasShield)
        {
            return false;
        }

        _hasShield = false;
        RpcPlayShieldPulse(true);
        return true;
    }

    [ClientRpc]
    private void RpcPlayShieldPulse(bool isBreaking)
    {
        if (_view)
        {
            _view.SetShieldVisible(!isBreaking);
            _view.PlayPulse(isBreaking);
        }
    }
}
