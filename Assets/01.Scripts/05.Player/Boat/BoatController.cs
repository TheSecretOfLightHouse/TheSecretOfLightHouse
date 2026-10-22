using System;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(NetworkIdentity), typeof(BoatContactBoarding))]
[RequireComponent(typeof(BoatDurability))]
public sealed class BoatController : NetworkBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    [SerializeField]
    private Transform _boardingAnchor;

    [SerializeField, Min(0f)]
    private float _speed = 8f;

    [SerializeField, Min(0f)]
    private float _acceleration = 4f;

    [SerializeField, Min(0f)]
    private float _recoilRecoveryDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float _turnSpeed = 45f;

    [SerializeField, Min(0.01f)]
    private float _reloadDuration = 2f;

    [SerializeField, Min(0.01f)]
    private float _boardingDistance = 1.5f;

    [SerializeField, Min(0f)]
    private float _departureProtectionDuration = 3f;

    [SerializeField, Min(0f)]
    private float _reboardDelay = 1f;

    [SerializeField, Min(0.01f)]
    private float _requestInterval = 0.2f;

    [SyncVar]
    private PlayerController _owner;

    private PlayerController _offlineOwner;

    [SyncVar]
    private float _departureHeading;

    [SyncVar]
    private Vector3 _landingPosition;

    [SyncVar(hook = nameof(OnOccupiedChanged))]
    private bool _isOccupied;

    private Collider _bodyCollider;
    private BoatContactBoarding _contact;
    private BoatDurability _durability;
    private float _nextLocalRequestTime;
    private float _nextServerRequestTime;
    private float _nextBoardTime;
    private float _nextDisembarkTime;
    private float _nextFireTime;
    private float _recoilEndTime;
    private bool _isWreckPresentationApplied;

    public float ReloadDuration => _reloadDuration;
    public PlayerController Owner => IsNetworkActive ? _owner : _offlineOwner;
    private bool IsNetworkActive => NetworkClient.active || NetworkServer.active;
    public bool IsOccupied => _isOccupied;
    public bool IsWrecked => _durability && _durability.IsWrecked;
    public Transform BoardingAnchor => _boardingAnchor ? _boardingAnchor : transform;
    public event Action<PlayerController> CannonFired;

    private DashModule _dashModule;
    [SerializeField, Min(1f)]
    private float _dashAccelerationMultiplier = 4f;

    private void Awake()
    {
        ResolveReferences();
        _dashModule = GetComponent<DashModule>();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (IsWrecked && !_isWreckPresentationApplied)
        {
            ApplyWreckPresentation();
        }

        if (_isOccupied && (isServer || !IsNetworkActive))
        {
            _contact.RefreshDepartureContacts();
        }
        if (Owner && ((_isOccupied && Owner.CurrentBoat != this) || (!_isOccupied && Owner.CurrentBoat == this)))
        {
            ApplyOccupiedState();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _rb.isKinematic = true;
        _durability.Wrecked += HandleWrecked;
    }

    public override void OnStopServer()
    {
        _durability.Wrecked -= HandleWrecked;
        base.OnStopServer();
    }

    private void HandleWrecked()
    {
        ApplyWreckPresentation();

        if (!Owner || !_isOccupied)
        {
            return;
        }

        if (!Owner.TryGetComponent(out PlayerWreckRecovery recovery))
        {
            Debug.LogError(
                $"[{nameof(BoatController)}] Missing {nameof(PlayerWreckRecovery)} on {Owner.name}.",
                Owner);

            Owner.SetDowned(true);
            return;
        }

        recovery.TryBeginRecovery(transform.position);
    }

    private void ApplyWreckPresentation()
    {
        if (_isWreckPresentationApplied)
        {
            return;
        }

        _isWreckPresentationApplied = true;
        Stop();

        foreach (Renderer boatRenderer in GetComponentsInChildren<Renderer>(true))
        {
            boatRenderer.enabled = false;
        }

        foreach (Collider boatCollider in GetComponentsInChildren<Collider>(true))
        {
            boatCollider.enabled = false;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _rb.isKinematic = !isOwned;
        ApplyOccupiedState();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        _rb.isKinematic = false;
        ApplyOccupiedState();
    }

    public override void OnStopAuthority()
    {
        Stop();
        _rb.isKinematic = true;
        base.OnStopAuthority();
    }

    [Server]
    public bool TryDisembarkForRecovery(PlayerController player, Vector3 recoveryPosition)
    {
        if (!player || Owner != player || !IsWrecked || !player.IsDowned)
        {
            return false;
        }

        if (!_isOccupied || player.CurrentBoat != this)
        {
            return false;
        }

        _landingPosition = recoveryPosition;
        _isOccupied = false;
        ApplyOccupiedState();

        if (player.IsOnBoat)
        {
            return false;
        }

        RpcDisembarkForRecovery(recoveryPosition);
        return true;
    }

    [ClientRpc]
    private void RpcDisembarkForRecovery(Vector3 recoveryPosition)
    {
        if (isServer || !Owner)
        {
            return;
        }

        Owner.ApplyDisembarking(this, recoveryPosition);
    }

    [Server]
    public bool TrySetOwner(PlayerController player)
    {
        if (!player || IsWrecked || (Owner && Owner != player))
        {
            return false;
        }

        _owner = player;
        return true;
    }

    public bool RequestBoard(PlayerController player)
    {
        if (!CanRequest(player) || _isOccupied || player.IsOnBoat)
        {
            return false;
        }

        _nextLocalRequestTime = Time.unscaledTime + _requestInterval;
        if (IsNetworkActive)
        {
            CmdRequestBoard();
        }
        else
        {
            TryBoardCore();
        }
        return player.CurrentBoat == this;
    }

    public bool RequestDisembark(
    PlayerController player,
    bool isManual = false)
    {
        if (!CanRequest(player)
            || !_isOccupied
            || player.CurrentBoat != this)
        {
            return false;
        }

        _nextLocalRequestTime = Time.unscaledTime + _requestInterval;

        if (IsNetworkActive)
        {
            CmdRequestDisembark(isManual);
        }
        else
        {
            TryDisembarkCore(isManual);
        }

        return player.CurrentBoat != this;
    }
    public void Disembark(PlayerController player)
    {
        if (player == Owner && (isOwned || !IsNetworkActive))
        {
            Stop();
        }
    }

    public void Move(PlayerController player, Vector2 input, MoveConfig config, float fixedDeltaTime)
    {
        if (!CanControl(player))
        {
            return;
        }

        if (config.InputLocked)
        {
            input = Vector2.zero;
        }

        float turn = Mathf.Clamp(input.x, -1f, 1f) * _turnSpeed * fixedDeltaTime;
        bool isSteering = !Mathf.Approximately(input.x, 0f);
        if (isSteering)
        {
            Vector3 angularVelocity = _rb.angularVelocity;
            angularVelocity.y = 0f;
            _rb.angularVelocity = angularVelocity;
        }
        Quaternion rotation = Quaternion.AngleAxis(turn, Vector3.up) * _rb.rotation;
        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
        bool isDashing = _dashModule && _dashModule.isActiveAndEnabled && _dashModule.IsDashing && !config.InputLocked && input.y > 0f;

        float dashMultiplier = isDashing ? _dashModule.SpeedMultiplier : 1f;

        Vector3 targetVelocity = forward * Mathf.Clamp(input.y, -1f, 1f) * _speed * config.SpeedMultiplier * dashMultiplier;
        Vector3 velocity = _rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (Time.time >= _recoilEndTime)
        {
            float recovery = _recoilEndTime > 0f && _recoilRecoveryDuration > 0f ? Mathf.SmoothStep(0f, 1f, (Time.time - _recoilEndTime) / _recoilRecoveryDuration) : 1f;
            if (isDashing)
            {
                horizontal = targetVelocity;
            }
            else
            {
                horizontal = Vector3.MoveTowards(horizontal, targetVelocity, _acceleration * config.AccelerationMultiplier * recovery * fixedDeltaTime);
            }
        }
        _rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

        if (!config.InputLocked)
        {
            _rb.MoveRotation(rotation);
        }
    }

    public bool TrySetHeading(PlayerController player, Quaternion rotation)
    {
        if (!CanControl(player))
        {
            return false;
        }

        _rb.rotation = rotation;
        _rb.angularVelocity = Vector3.zero;
        return true;
    }

    public void PreserveCollisionRecoil(float duration)
    {
        _recoilEndTime = Mathf.Max(_recoilEndTime, Time.time + duration);
    }

    public bool TryFire(PlayerController player)
    {
        if (!CanControl(player) || Time.time < _nextFireTime)
        {
            return false;
        }

        _nextFireTime = Time.time + _reloadDuration;
        CannonFired?.Invoke(player);
        return true;
    }

    public void Stop()
    {
        _recoilEndTime = 0f;

        if (!_rb || _rb.isKinematic)
        {
            return;
        }

        Vector3 velocity = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
        _rb.angularVelocity = Vector3.zero;
    }

    [Command]
    private void CmdRequestBoard()
    {
        if (CanAcceptRequest())
        {
            TryBoardCore();
        }
    }

    private void TryBoardCore()
    {
        if (_isOccupied || Owner.IsOnBoat || Time.time < _nextBoardTime || !_bodyCollider)
        {
            return;
        }

        if (!_contact || !_contact.TryGetDockZone(out DockZone dock) || !dock.ContainsPosition(Owner.transform.position))
        {
            return;
        }

        Vector3 point = _bodyCollider.ClosestPoint(Owner.transform.position);
        if ((point - Owner.transform.position).sqrMagnitude
            > _boardingDistance * _boardingDistance)
        {
            return;
        }

        _departureHeading = _contact.GetDepartureHeading();
        _contact.BeginDepartureProtection();
        _nextDisembarkTime = Time.time + _departureProtectionDuration;
        _isOccupied = true;
        ApplyOccupiedState();
    }

    [Command]
    private void CmdRequestDisembark(bool isManual)
    {
        if (CanAcceptRequest())
        {
            TryDisembarkCore(isManual);
        }
    }

    private void TryDisembarkCore(bool isManual = false)
    {
        if (!_isOccupied
        || Time.time < _nextDisembarkTime)
        {
            return;
        }

        if (!_contact || !_contact.TryGetDockZone(out DockZone dock))
        {
            return;
        }

        Vector3 position;
        bool hasLandingPosition;

        if (isManual)
        {
            hasLandingPosition =
                _contact.TryFindDockLandingPosition(Owner, out position);
        }
        else
        {
            hasLandingPosition =
                _contact.TryFindLandingPosition(Owner, out position);
        }

        if (!hasLandingPosition)
        {
            return;
        }

        _landingPosition = position;
        _nextBoardTime = Time.time + _reboardDelay;
        _isOccupied = false;
        ApplyOccupiedState();
    }

    private void OnOccupiedChanged(bool oldValue, bool newValue)
    {
        ApplyOccupiedState();
    }

    private void ApplyOccupiedState()
    {
        if (!Owner || !Owner.isActiveAndEnabled)
        {
            return;
        }

        if (_isOccupied)
        {
            bool newlyBoarded = Owner.CurrentBoat != this;
            if (Owner.ApplyBoarding(this) && newlyBoarded && (isOwned || !IsNetworkActive))
            {
                Owner.SetBoatHeading(Quaternion.Euler(0f, _departureHeading, 0f) * Vector3.forward);
            }
        }
        else
        {
            Owner.ApplyDisembarking(this, _landingPosition);
        }
    }

    private bool CanRequest(PlayerController player)
    {
        return !IsWrecked && (!IsNetworkActive || (isClient && isOwned)) && isActiveAndEnabled && player
            && player == Owner && player.CanProcessBoatInput
            && player.isActiveAndEnabled && !player.IsDowned
            && Time.unscaledTime >= _nextLocalRequestTime;
    }

    private bool CanAcceptRequest()
    {
        if (IsWrecked || !Owner || !Owner.isActiveAndEnabled || Owner.IsDowned || connectionToClient == null
            || Owner.connectionToClient != connectionToClient
            || connectionToClient.identity != Owner.netIdentity
            || Time.time < _nextServerRequestTime)
        {
            return false;
        }

        _nextServerRequestTime = Time.time + _requestInterval;
        return true;
    }

    private bool CanControl(PlayerController player)
    {
        return !IsWrecked && (!IsNetworkActive || (isClient && isOwned)) && isActiveAndEnabled && player
            && player == Owner && player.CanProcessBoatInput
            && player.isActiveAndEnabled && !player.IsDowned
            && _isOccupied && player.CurrentBoat == this;
    }

    private void ResolveReferences()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }

        _bodyCollider = GetComponent<Collider>();
        _contact = GetComponent<BoatContactBoarding>();
        _durability = GetComponent<BoatDurability>();
    }

    public bool TrySetOfflineOwner(PlayerController player)
    {
        if (IsNetworkActive || !player || !player.CanProcessBoatInput || (Owner && Owner != player))
        {
            return false;
        }

        PlayerController previousOwner = Owner;
        _offlineOwner = player;
        if (player.TryAssignOfflineOwnedBoat(this))
        {
            return true;
        }
       
        _offlineOwner = previousOwner;
        return false;
    }
}
