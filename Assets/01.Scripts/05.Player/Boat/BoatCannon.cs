using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController))]
public sealed class BoatCannon : NetworkBehaviour
{
    [SerializeField]
    private Transform _muzzle;

    [SerializeField]
    private Cannonball _projectilePrefab;

    [SerializeField, Min(0.01f)]
    private float _launchSpeed = 20f;

    [SerializeField, Min(0.01f)]
    private float _damage = 10f;

    private BoatController _boat;
    private Collider[] _boatColliders;
    private double _nextServerFireTime;

    public event System.Action Fired;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _boatColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        _boat.CannonFired += HandleCannonFired;
    }

    private void OnDisable()
    {
        _boat.CannonFired -= HandleCannonFired;
    }

    private void HandleCannonFired(PlayerController player)
    {
        if (!isClient || !isOwned || player != _boat.Owner)
        {
            return;
        }

        CmdRequestFire();
    }

    [Command]
    private void CmdRequestFire(NetworkConnectionToClient sender = null)
    {
        LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

        if (!manager)
        {
            return;
        }

        if (manager.IsSessionFlowEnabled && manager.Phase != MatchPhase.InProgress)
        {
            return;
        }

        PlayerController owner = _boat.Owner;

        if (sender == null || !owner)
        {
            return;
        }

        if (owner.connectionToClient != sender || sender.identity != owner.netIdentity)
        {
            return;
        }

        if (!_boat.isActiveAndEnabled || !owner.isActiveAndEnabled || !_boat.IsOccupied || owner.CurrentBoat != _boat
            || owner.IsDowned || _boat.IsWrecked)
        {
            return;
        }

        if (!_muzzle || !_projectilePrefab)
        {
            return;
        }

        if (NetworkTime.time < _nextServerFireTime)
        {
            return;
        }

        float reloadDurationMultiplier = 1f;

        if (owner.TryGetComponent(out PlayerPassiveController passiveController))
        {
            reloadDurationMultiplier = passiveController.GetReloadDurationMultiplier(_boat);
        }


        float reloadDuration = _boat.ReloadDuration * reloadDurationMultiplier;

        _nextServerFireTime = NetworkTime.time + reloadDuration;

        Cannonball projectile = Instantiate(_projectilePrefab, _muzzle.position, _muzzle.rotation);

        projectile.Initialize(_muzzle.forward * _launchSpeed,_damage, _boatColliders, owner.GetComponentsInChildren<Collider>(true));

        NetworkServer.Spawn(projectile.gameObject);
        RpcPlayFireEffects();
    }

    [ClientRpc]
    private void RpcPlayFireEffects()
    {
        PlayerController owner = _boat.Owner;
        if (owner && owner.isOwned && owner.TryGetComponent(out PlayerActionEvents actionEvents))
        {
            actionEvents.Publish(PlayerAnimationAction.Fire);
        }

        Fired?.Invoke();
    }
}
