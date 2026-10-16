using Mirror;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Lighthouse.Network.Connections.Net;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerAbilityRunner))]
public sealed class PlayerController : NetworkBehaviour, IPlayerAbilityContext
{
    [SerializeField]
    private PlayerInputReader _inputReader;

    [SerializeField]
    private PlayerMotor _motor;

    [SerializeField]
    private PlayerAbilityRunner _abilityRunner;

    [SerializeField]
    private PlayerAbility _jobAbility;

    [SerializeField]
    private PlayerAnimationDriver _animationDriver;

    [SerializeField]
    private PlayerCamera _camera;

    private PlayerAnimationSource _animationSource;

    [SerializeField, Min(1f)]
    private float _runMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float _mouseSensitivity = 0.15f;

    [SerializeField]
    private bool _allowOfflineControl = true;

    [SyncVar]
    private BoatController _ownedBoat;
    private BoatController _offlineOwnedBoat;

    [SyncVar(hook = nameof(OnDownedChanged))]
    private bool _isDowned;

    private PlayerHFSM _hfsm;
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private InputAction _lookAction;
    private NetworkBehaviour _playerNetworkTransform;
    private bool _savedNetworkTransformEnabled;
    private BoatController _boatController;

    private bool _initialIsKinematic;
    private bool _savedIsKinematic;
    private bool _savedDetectCollisions;

    private bool _interactRequested;
    private bool _harvestRequested;
    private bool _fireRequested;
    private bool _abilityRequested;
    private bool _jumpRequested;
    private float _heading;

    public Transform Actor => transform;

    public bool IsDowned => _hfsm.IsDowned;
    public bool IsOnBoat => _boatController != null;

    public BoatController OwnedBoat => NetworkClient.active || NetworkServer.active ? _ownedBoat : _offlineOwnedBoat;
    public BoatController CurrentBoat => _boatController;
    public bool CanProcessBoatInput => CanProcessLocalInput;
    public PlayerHFSM StateMachine => _hfsm;

    private bool CanProcessLocalInput
    {
        get
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return isLocalPlayer;
            }

            return _allowOfflineControl;
        }
    }

    public event Action<PlayerController> InteractionRequested;
    public event Action<PlayerController> HarvestRequested;
    public event Action<BoatController> Boarded;
    public event Action<BoatController> Disembarked;
    public event Action<bool> DownedChanged;

    private NetworkActor _networkActor;
    public uint ActorID => _networkActor ? _networkActor.ActorId : 0;

    private void Awake()
    {
        ResolveReferences();
        _rb = GetComponent<Rigidbody>();
        _initialIsKinematic = _rb.isKinematic;
        _playerInput = GetComponent<PlayerInput>();
        CacheNetworkTransform();
        _hfsm = new PlayerHFSM(_runMultiplier);
        _networkActor = GetComponent<NetworkActor>();

        if (_animationDriver)
        {
            _animationSource = new PlayerAnimationSource(_hfsm, _abilityRunner);
            _animationDriver.Initialize(_animationSource);
        }
    }

    private void OnEnable()
    {
        _heading = transform.eulerAngles.y;

        _inputReader.InteractRequested += HandleInteractRequested;
        _inputReader.HarvestRequested += HandleHarvestRequested;
        _inputReader.FireRequested += HandleFireRequested;
        _inputReader.AbilityRequested += HandleAbilityRequested;
        _inputReader.JumpRequested += HandleJumpRequested;

        RefreshLocalControl();
    }

    private void Start()
    {
        if (!NetworkClient.active&& !NetworkServer.active&& _allowOfflineControl)
        {
            _camera?.Attach();
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void CacheNetworkTransform()
    {
        foreach (NetworkBehaviour behaviour in GetComponents<NetworkBehaviour>())
        {
            Type type = behaviour.GetType();
            while (type != null)
            {
                if (type.Name == "NetworkTransformBase")
                {
                    _playerNetworkTransform = behaviour;
                    return;
                }

                type = type.BaseType;
            }
        }
    }

    private void ResolveReferences()
    {
        if (!_inputReader)
        {
            _inputReader = GetComponent<PlayerInputReader>();
        }
        if (!_motor)
        {
            _motor = GetComponent<PlayerMotor>();
        }
        if (!_abilityRunner)
        {
            _abilityRunner = GetComponent<PlayerAbilityRunner>();
        }
        if (!_animationDriver)
        {
            _animationDriver = GetComponent<PlayerAnimationDriver>();
        }
        if (!_camera)
        {
            _camera = GetComponent<PlayerCamera>();
        }
    }

    private void Update()
    {
        if (!CanProcessLocalInput)
        {
            return;
        }

        if (_hfsm.IsOnBoat && !IsOnBoat)
        {
            LeaveBoat(transform.position);
            SetDowned(true);
        }

        if (!IsDowned)
        {
            Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            _heading = Mathf.Repeat(_heading + look.x * _mouseSensitivity, 360f);
            if (_camera)
            {
                _camera.ApplyVerticalLook(look.y * _mouseSensitivity);
            }
        }

        Vector2 input = _inputReader.MoveInput;

        Vector3 direction = Quaternion.Euler(0f, _heading, 0f) * new Vector3(input.x, 0f, input.y);

        _hfsm.SetInput(direction, _inputReader.RunHeld);
        _hfsm.SetJumping(_motor.IsJumping);
        _hfsm.Tick(Time.deltaTime);

        ProcessActionRequests();

        if (isActiveAndEnabled)
        {
            _abilityRunner.Tick(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (!CanProcessLocalInput)
        {
            return;
        }
        float fixedDeltaTime = Time.fixedDeltaTime;
        _hfsm.FixedTick(fixedDeltaTime);

        MoveConfig config = MoveConfig.Combine(_hfsm.CurrentMoveConfig,_abilityRunner.MovementModifier);

        bool jumpRequested = _jumpRequested;
        _jumpRequested = false;

        Quaternion rotation = Quaternion.Euler(0f, _heading, 0f);

        if (IsOnBoat)
        {
            _boatController.Move(this, _hfsm.MoveDirection,rotation, config,fixedDeltaTime);
            return;
        }

        if (!IsDowned)
        {
            _motor.Face(rotation);
        }

        _motor.Move(_hfsm.MoveDirection, config,fixedDeltaTime);

        if (jumpRequested && !IsDowned && !config.InputLocked)
        {
            _motor.TryJump();
        }
    }

    private void LateUpdate()
    {
        if (!IsOnBoat)
        {
            return;
        }

        Transform anchor = _boatController.BoardingAnchor;
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    private void OnDisable()
    {
        if (_inputReader)
        {
            _inputReader.InteractRequested -= HandleInteractRequested;
            _inputReader.HarvestRequested -= HandleHarvestRequested;
            _inputReader.FireRequested -= HandleFireRequested;
            _inputReader.AbilityRequested -= HandleAbilityRequested;
            _inputReader.JumpRequested -= HandleJumpRequested;
        }

        ClearActionRequests();

        if (_abilityRunner)
        {
            _abilityRunner.Cancel();
        }

        if (_hfsm != null && _hfsm.IsOnBoat)
        {
            LeaveBoat(transform.position);
        }
    }
    private void OnDestroy()
    {
        _animationSource?.Dispose();
        _animationSource = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RefreshLocalControl();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyDownedState(_isDowned);
        RefreshLocalControl();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _heading = transform.eulerAngles.y;
        RefreshLocalControl();
        _camera?.Attach();
    }

    public override void OnStopLocalPlayer()
    {
        _camera.Release();
        base.OnStopLocalPlayer();
    }

    public override void OnStopClient()
    {
        _camera.Release();
        base.OnStopClient();
    }

    #region Boat

    public bool TryBoard(BoatController boat)
    {
        return boat && boat.RequestBoard(this);
    }

    public bool ApplyBoarding(BoatController boat)
    {
        if (!isActiveAndEnabled || !boat || boat.Owner != this || !boat.IsOccupied)
        {
            return false;
        }

        if (_boatController == boat)
        {
            return true;
        }

        if (IsOnBoat)
        {
            return false;
        }

        _abilityRunner.Cancel();
        ClearActionRequests();
        _savedIsKinematic = _rb.isKinematic;
        _savedDetectCollisions = _rb.detectCollisions;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;
        _rb.detectCollisions = false;

        if (_playerNetworkTransform)
        {
            _savedNetworkTransformEnabled = _playerNetworkTransform.enabled;
            _playerNetworkTransform.enabled = false;
        }

        _boatController = boat;
        _heading = boat.transform.eulerAngles.y;
        Transform anchor = boat.BoardingAnchor;
        _rb.position = anchor.position;
        _rb.rotation = anchor.rotation;
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        _hfsm.SetOnBoat(true);
        RefreshState();
        Boarded?.Invoke(boat);
        return true;
    }

    public void ApplyDisembarking(BoatController boat, Vector3 position)
    {
        if (_boatController != boat)
        {
            return;
        }

        _heading = boat.transform.eulerAngles.y;
        LeaveBoat(position);
    }

    public bool TryDisembark(Vector3 landingPosition)
    {
        return _boatController && _boatController.RequestDisembark(this);
    }

    [Server]
    public bool TryAssignOwnedBoat(BoatController boat)
    {
        return AssignOwnedBoat(boat);
    }

    public bool TryAssignOfflineOwnedBoat(BoatController boat)
    {
        return !NetworkClient.active && !NetworkServer.active && AssignOwnedBoat(boat);
    }

    private bool AssignOwnedBoat(BoatController boat)
    {
        if (!boat || boat.Owner != this)
        {
            return false;
        }

        if (OwnedBoat)
        {
            return OwnedBoat == boat;
        }

        if (NetworkClient.active || NetworkServer.active)
        {
            _ownedBoat = boat;
        }
        else
        {
            _offlineOwnedBoat = boat;
        }
        return true;
    }

    public void SetBoatHeading(Vector3 direction)
    {
        if (!CanProcessLocalInput || !IsOnBoat || IsDowned)
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (_boatController.TrySetHeading(this, rotation))
        {
            _heading = rotation.eulerAngles.y;
        }
    }

    private void LeaveBoat(Vector3 landingPosition)
    {
        BoatController previousBoat = _boatController;

        if (_abilityRunner)
        {
            _abilityRunner.Cancel();
        }

        ClearActionRequests();

        if (previousBoat)
        {
            previousBoat.Disembark(this);
        }

        _boatController = null;

        _rb.position = landingPosition;
        _rb.rotation = Quaternion.Euler(0f, _heading, 0f);
        transform.SetPositionAndRotation(_rb.position, _rb.rotation);

        _rb.isKinematic = CanProcessLocalInput ? _savedIsKinematic : true;
        _rb.detectCollisions = _savedDetectCollisions;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (_playerNetworkTransform)
        {
            _playerNetworkTransform.enabled = _savedNetworkTransformEnabled;
        }

        _hfsm.SetOnBoat(false);
        RefreshState();

        Disembarked?.Invoke(previousBoat);
    }

    #endregion

    public void SetDowned(bool value)
    {
        if (NetworkClient.active || NetworkServer.active)
        {
            if (!isServer)
            {
                return;
            }

            _isDowned = value;
        }

        ApplyDownedState(value);
    }

    private void OnDownedChanged(bool oldValue, bool newValue)
    {
        ApplyDownedState(newValue);
    }

    private void ApplyDownedState(bool value)
    {
        if (IsDowned == value)
        {
            return;
        }

        if (value)
        {
            _abilityRunner.Cancel();
            ClearActionRequests();

            if (IsOnBoat)
            {
                _boatController.Stop();
            }
        }

        _hfsm.SetDowned(value);
        RefreshState();

        DownedChanged?.Invoke(value);
    }

    public void SetJobAbility(PlayerAbility ability)
    {
        _abilityRunner.Cancel();
        _jobAbility = ability;
    }

    private void ProcessActionRequests()
    {
        bool interactRequested = _interactRequested;
        bool harvestRequested = _harvestRequested;
        bool fireRequested = _fireRequested;
        bool abilityRequested = _abilityRequested;

        _interactRequested = false;
        _harvestRequested = false;
        _fireRequested = false;
        _abilityRequested = false;

        if (IsDowned)
        {
            return;
        }

        if (interactRequested)
        {
            InteractionRequested?.Invoke(this);
        }

        if (IsDowned || !isActiveAndEnabled)
        {
            return;
        }

        if (harvestRequested && !IsOnBoat)
        {
            HarvestRequested?.Invoke(this);
        }

        if (IsDowned || !isActiveAndEnabled)
        {
            return;
        }

        if (fireRequested && IsOnBoat)
        {
            _boatController.TryFire(this);
        }

        if (IsDowned || !isActiveAndEnabled)
        {
            return;
        }

        if (abilityRequested)
        {
            _abilityRunner.TryExecute(_jobAbility, this);
        }
    }

    private void RefreshState()
    {
        _hfsm.SetInput(Vector3.zero, false);
        _hfsm.Tick(0f);
    }

    private void HandleInteractRequested()
    {
        _interactRequested = true;
    }

    private void HandleHarvestRequested()
    {
        _harvestRequested = true;
    }

    private void HandleFireRequested()
    {
        _fireRequested = true;
    }

    private void HandleAbilityRequested()
    {
        _abilityRequested = true;
    }

    private void ClearActionRequests()
    {
        _interactRequested = false;
        _harvestRequested = false;
        _fireRequested = false;
        _abilityRequested = false;
        _jumpRequested = false;
    }

    private void HandleJumpRequested()
    {
        if (CanProcessLocalInput && !IsDowned && !IsOnBoat)
        {
            _jumpRequested = true;
        }
    }

    private void RefreshLocalControl()
    {
        bool canControl = CanProcessLocalInput;

        if (_playerInput)
        {
            _playerInput.enabled = canControl;
            _lookAction = _playerInput.actions ? _playerInput.actions.FindAction("Look", false) : null;
        }

        _inputReader.enabled = canControl;

        if (!IsOnBoat)
        {
            _rb.isKinematic = canControl ? _initialIsKinematic : true;
        }

        if (!canControl)
        {
            ClearActionRequests();
        }
    }
}
