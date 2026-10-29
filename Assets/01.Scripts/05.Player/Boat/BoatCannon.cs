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
    private Rigidbody _boatBody;
    private Collider[] _boatColliders;
    private double _nextServerFireTime;
    private Vector3 _previousMuzzlePosition;
    private Vector3 _muzzleVelocity;
    private double _previousSampleTime;
    private bool _hasMotionSample;

    public event System.Action Fired;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _boatBody = GetComponent<Rigidbody>();
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        _hasMotionSample = false;
        _muzzleVelocity = Vector3.zero;
        SampleMuzzleVelocity();
    }

    private void LateUpdate()
    {
        if (isServer)
        {
            SampleMuzzleVelocity();
        }
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
        float towerDamageMultiplier = 1f;

        if (owner.TryGetComponent(out PlayerPassiveController passiveController))
        {
            reloadDurationMultiplier = passiveController.GetReloadDurationMultiplier(_boat);
            towerDamageMultiplier = passiveController.GetTowerCannonDamageMultiplier();
        }


        float reloadDuration = _boat.ReloadDuration * reloadDurationMultiplier;

        _nextServerFireTime = NetworkTime.time + reloadDuration;

        Cannonball projectile = Instantiate(_projectilePrefab, _muzzle.position, _muzzle.rotation);

        projectile.Initialize(GetLaunchVelocity(), _damage * towerDamageMultiplier, _boatColliders, owner.GetComponentsInChildren<Collider>(true));

        NetworkServer.Spawn(projectile.gameObject);
        RpcPlayFireEffects();
    }

    private Vector3 GetLaunchVelocity()
    {
        SampleMuzzleVelocity();

        // Remote boats are kinematic on the server, so their Rigidbody velocity is not authoritative motion.
        Vector3 inheritedVelocity = _boatBody.isKinematic
            ? _muzzleVelocity
            : _boatBody.GetPointVelocity(_muzzle.position);

        return _muzzle.forward * _launchSpeed + inheritedVelocity;
    }

    private void SampleMuzzleVelocity()
    {
        if (!_muzzle)
        {
            return;
        }

        double now = Time.timeAsDouble;
        Vector3 position = _muzzle.position;

        if (_hasMotionSample)
        {
            double elapsed = now - _previousSampleTime;
            if (elapsed <= 0d)
            {
                return;
            }

            _muzzleVelocity = (position - _previousMuzzlePosition) / (float)elapsed;
        }

        _previousMuzzlePosition = position;
        _previousSampleTime = now;
        _hasMotionSample = true;
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
