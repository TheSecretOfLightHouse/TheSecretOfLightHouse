using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatDurability))]
public sealed class BoatShield : NetworkBehaviour
{
    [SyncVar]
    private bool _hasShield;

    private BoatDurability _durability;

    public bool HasShield => _hasShield;

    private void Awake()
    {
        _durability = GetComponent<BoatDurability>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _hasShield = false;
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
        return true;
    }
}
