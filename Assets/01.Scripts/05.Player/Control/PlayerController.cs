using Mirror;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Query.Rules;

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

    [SerializeField]
    private bool _requireLocalWorldReady = true;

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
    private System.Collections.IDictionary _serverMovementSnapshots;
    private System.Collections.IDictionary _clientMovementSnapshots;
    private BoatController _boatController;

    private bool _initialIsKinematic;
    private bool _savedIsKinematic;
    private bool _savedDetectCollisions;
    private bool _unlockedIsKinematic;
    private bool _canRunLocalWorld;
    private bool _hasStoppedClient;

    private bool _isRescuePromptOpen;
    private int _rescuePromptClosedFrame = -1;
    private bool _interactRequested;
    private bool _harvestRequested;
    private bool _fireRequested;
    private bool _abilityRequested;
    private bool _jumpRequested;
    private float _heading;
    private bool _hasSavedCursor;
    private CursorLockMode _savedCursorLockMode;
    private bool _savedCursorVisible;

    public Transform Actor => transform;

    public bool IsDowned => _hfsm.IsDowned;
    public bool IsOnBoat => _boatController != null;

    public BoatController OwnedBoat => NetworkClient.active || NetworkServer.active ? _ownedBoat : _offlineOwnedBoat;
    public BoatController CurrentBoat => _boatController;
    public bool CanProcessBoatInput => CanProcessLocalInput;
    public bool CanSimulateLocalWorld =>
        isActiveAndEnabled
        && !_hasStoppedClient
        && HasLocalControlAuthority
        && CanRunLocalWorld();
    public PlayerHFSM StateMachine => _hfsm;

    private bool HasLocalControlAuthority
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

    private bool CanProcessLocalInput =>
        isActiveAndEnabled
        && !_hasStoppedClient
        && HasLocalControlAuthority
        && _canRunLocalWorld;

    public event Action<PlayerController> InteractionRequested;
    public event Action<PlayerController> HarvestRequested;
    public event Action<PlayerController> AbilityRequested;
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
        _unlockedIsKinematic = _initialIsKinematic;
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
        _inputReader.DashRequested += HandleDashRequested;

        RefreshLocalControl();

        if (CanProcessLocalInput)
        {
            ConfineCursor();
        }
    }

    private void Start()
    {
        if (!NetworkClient.active && !NetworkServer.active&& _allowOfflineControl)
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
                    // Mirror components live outside this runtime assembly's references.
                    _serverMovementSnapshots = type.GetField("serverSnapshots").GetValue(behaviour)
                        as System.Collections.IDictionary;
                    _clientMovementSnapshots = type.GetField("clientSnapshots").GetValue(behaviour)
                        as System.Collections.IDictionary;
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
        RefreshWorldReadiness();

        if (!CanProcessLocalInput)
        {
            return;
        }

        if (_hfsm.IsOnBoat && !IsOnBoat)
        {
            LeaveBoat(transform.position);
            SetDowned(true);
        }

        if (!IsDowned && !_isRescuePromptOpen)
        {
            Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            if (IsOnBoat)
            {
                if (_camera)
                {
                    _camera.ApplyHorizontalLook(look.x * _mouseSensitivity);
                }
            }
            else
            {
                _heading = Mathf.Repeat(_heading + look.x * _mouseSensitivity, 360f);
            }
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
        RefreshWorldReadiness();

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
            _boatController.Move(this, _inputReader.MoveInput, config, fixedDeltaTime);
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
        RestoreCursor();

        if (_inputReader)
        {
            _inputReader.InteractRequested -= HandleInteractRequested;
            _inputReader.HarvestRequested -= HandleHarvestRequested;
            _inputReader.FireRequested -= HandleFireRequested;
            _inputReader.AbilityRequested -= HandleAbilityRequested;
            _inputReader.JumpRequested -= HandleJumpRequested;
            _inputReader.DashRequested -= HandleDashRequested;
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

        RefreshLocalControl();
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

        _hasStoppedClient = false;
        ApplyDownedState(_isDowned);
        RefreshLocalControl();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _heading = transform.eulerAngles.y;
        RefreshLocalControl();
        _camera?.Attach();
        ConfineCursor();
    }

    public override void OnStopLocalPlayer()
    {
        RestoreCursor();
        _camera.Release();
        base.OnStopLocalPlayer();
    }

    private void ConfineCursor()
    {
        if (!_hasSavedCursor)
        {
            _savedCursorLockMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _hasSavedCursor = true;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!_hasSavedCursor)
        {
            return;
        }

        _hasSavedCursor = false;
        Cursor.lockState = _savedCursorLockMode;
        Cursor.visible = _savedCursorVisible;
    }

    public override void OnStopClient()
    {
        _hasStoppedClient = true;
        RefreshLocalControl();

        if (_camera != null)
        {
            _camera.Release();
        }

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
        _savedIsKinematic = _unlockedIsKinematic;
        _savedDetectCollisions = _rb.detectCollisions;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;
        _rb.detectCollisions = false;

        ClearMovementSnapshots();

        _boatController = boat;
        _heading = boat.transform.eulerAngles.y;
        if (CanProcessLocalInput && _camera)
        {
            _camera.SetBoatOrbit(true, _heading);
        }
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

    [Server]
    public bool TryReplaceWreckedBoat(
    BoatController wreckedBoat,
    BoatController replacementBoat)
    {
        if (!IsDowned || IsOnBoat)
        {
            return false;
        }

        if (!wreckedBoat || OwnedBoat != wreckedBoat
            || wreckedBoat.Owner != this || !wreckedBoat.IsWrecked)
        {
            return false;
        }

        if (!replacementBoat || replacementBoat == wreckedBoat
            || replacementBoat.Owner != this
            || replacementBoat.IsWrecked
            || replacementBoat.IsOccupied)
        {
            return false;
        }

        _ownedBoat = replacementBoat;
        return true;
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
            if (_camera)
            {
                _camera.SetBoatOrbit(true, _heading);
            }
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
        if (CanProcessLocalInput && _camera)
        {
            _camera.SetBoatOrbit(false, _heading);
        }

        _rb.position = landingPosition;
        _rb.rotation = Quaternion.Euler(0f, _heading, 0f);
        transform.SetPositionAndRotation(_rb.position, _rb.rotation);

        _unlockedIsKinematic = _savedIsKinematic;
        _rb.detectCollisions = _savedDetectCollisions;
        ApplyLocalPhysics();

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        ClearMovementSnapshots();

        _hfsm.SetOnBoat(false);
        RefreshState();

        Disembarked?.Invoke(previousBoat);
    }

    #endregion

    private void ClearMovementSnapshots()
    {
        if (!_playerNetworkTransform)
        {
            return;
        }

        _serverMovementSnapshots?.Clear();
        _clientMovementSnapshots?.Clear();
    }

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

    public void SetRescuePromptOpen(bool isOpen)
    {
        if (_isRescuePromptOpen == isOpen)
        {
            return;
        }

        _isRescuePromptOpen = isOpen;
        ClearActionRequests();

        if (!isOpen)
        {
            // The button release must not become a gameplay action in the same frame.
            _rescuePromptClosedFrame = Time.frameCount;
        }
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

        if (IsDowned || _isRescuePromptOpen
            || Time.frameCount <= _rescuePromptClosedFrame)
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
            AbilityRequested?.Invoke(this);
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

    private void HandleDashRequested()
    {
        if (!CanProcessLocalInput || IsDowned || !IsOnBoat)
        {
            return;
        }

        if (_boatController.TryGetComponent(out DashModule dashModule))
        {
            dashModule.RequestDash();
        }
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
        _canRunLocalWorld = CanRunLocalWorld();

        bool canControl = CanProcessLocalInput;

        if (_playerInput != null)
        {
            _playerInput.enabled = canControl;
            _lookAction = _playerInput.actions != null
                ? _playerInput.actions.FindAction("Look", false)
                : null;
        }

        if (_inputReader != null)
        {
            _inputReader.enabled = canControl;
        }

        ApplyLocalPhysics();

        if (!canControl)
        {
            ClearActionRequests();
        }
    }

    private void RefreshWorldReadiness()
    {
        bool canRunLocalWorld = CanRunLocalWorld();

        if (_canRunLocalWorld == canRunLocalWorld)
        {
            return;
        }

        RefreshLocalControl();
    }

    private bool CanRunLocalWorld()
    {
        if (_hasStoppedClient)
        {
            return false;
        }

        if (!NetworkClient.active && !NetworkServer.active)
        {
            return true;
        }

        LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

        if (manager == null)
        {
            return false;
        }

        if (manager.IsSessionFlowEnabled)
        {
            MatchPhase phase = NetworkServer.active
                ? manager.Phase
                : manager.ClientState.Phase;

            if (phase != MatchPhase.InProgress)
            {
                return false;
            }
        }

        if (!_requireLocalWorldReady)
        {
            return true;
        }

        return WorldServices.TryGet<MapStateNet>(out MapStateNet map)
            && map != null
            && map.isActiveAndEnabled
            && map.IsLayoutReady;
    }

    private void ApplyLocalPhysics()
    {
        if (_rb == null)
        {
            return;
        }

        bool isKinematic = !CanProcessLocalInput
            || IsOnBoat
            || _unlockedIsKinematic;

        if (_rb.isKinematic == isKinematic)
        {
            return;
        }

        if (isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = isKinematic;
    }
}
