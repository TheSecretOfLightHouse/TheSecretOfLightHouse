using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerAbilityRunner))]
public sealed class PlayerController : MonoBehaviour, IPlayerAbilityContext
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    [SerializeField]
    private PlayerInputReader _inputReader;

    [SerializeField]
    private PlayerMotor _motor;

    [SerializeField]
    private PlayerAbilityRunner _abilityRunner;

    [SerializeField]
    private PlayerAbility _jobAbility;
    [SerializeField]
    private Animator _animator;

    [SerializeField, Min(1f)]
    private float _runMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float _mouseSensitivity = 0.15f;

    private PlayerHFSM _hfsm;
    private Rigidbody _rb;
    private BoatController _boatController;

    private bool _savedIsKinematic;
    private bool _savedDetectCollisions;

    private bool _interactRequested;
    private bool _fireRequested;
    private bool _abilityRequested;
    private float _heading;

    public Transform Actor => transform;

    public bool IsDowned => _hfsm.IsDowned;
    public bool IsOnBoat => _boatController != null;

    public BoatController CurrentBoat => _boatController;
    public PlayerHFSM StateMachine => _hfsm;

    public event Action<PlayerController> InteractionRequested;
    public event Action<BoatController> Boarded;
    public event Action<BoatController> Disembarked;
    public event Action<bool> DownedChanged;

    private void Awake()
    {
        ResolveReferences();
        _rb = GetComponent<Rigidbody>();
        _hfsm = new PlayerHFSM(_runMultiplier);

        _hfsm.MotionChanged += HandleMotionChanged;
        HandleMotionChanged(_hfsm.CurrentMotion);
    }

    private void OnEnable()
    {
        _heading = transform.eulerAngles.y;

        _inputReader.InteractRequested += HandleInteractRequested;
        _inputReader.FireRequested += HandleFireRequested;
        _inputReader.AbilityRequested += HandleAbilityRequested;
    }

    private void Reset()
    {
        ResolveReferences();
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
        if (!_animator)
        {
            _animator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (_hfsm.IsOnBoat && !IsOnBoat)
        {
            LeaveBoat(transform.position);
            SetDowned(true);
        }

        if (!IsDowned)
        {
            _heading = Mathf.Repeat(_heading+ _inputReader.LookInput.x * _mouseSensitivity,360f);
        }

        Vector2 input = _inputReader.MoveInput;

        Vector3 direction = Quaternion.Euler(0f, _heading, 0f) * new Vector3(input.x, 0f, input.y);

        _hfsm.SetInput(direction, _inputReader.RunHeld);
        _hfsm.Tick(Time.deltaTime);

        ProcessActionRequests();

        if (isActiveAndEnabled)
        {
            _abilityRunner.Tick(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;
        _hfsm.FixedTick(fixedDeltaTime);

        MoveConfig config = MoveConfig.Combine(_hfsm.CurrentMoveConfig,_abilityRunner.MovementModifier);

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
            _inputReader.FireRequested -= HandleFireRequested;
            _inputReader.AbilityRequested -= HandleAbilityRequested;
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
        if (_hfsm != null)
        {
            _hfsm.MotionChanged -= HandleMotionChanged;
        }
    }

    private void HandleMotionChanged(PlayerMotionState state)
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        bool isRunning = state == PlayerMotionState.Run;
        bool isMoving = state == PlayerMotionState.Walk || isRunning;

        _animator.SetBool(IsMovingHash, isMoving);
        _animator.SetBool(IsRunningHash, isRunning);
    }

    public bool TryBoard(BoatController boat)
    {
        if (!isActiveAndEnabled|| !boat|| IsOnBoat|| IsDowned)
        {
            return false;
        }

        if (!boat.TryBoard(this))
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

        _boatController = boat;
        _heading = boat.transform.eulerAngles.y;

        Transform anchor = boat.BoardingAnchor;

        _rb.position = anchor.position;
        _rb.rotation = anchor.rotation;

        _hfsm.SetOnBoat(true);
        RefreshState();

        Boarded?.Invoke(boat);
        return true;
    }

    public bool TryDisembark(Vector3 landingPosition)
    {
        if (!isActiveAndEnabled || !IsOnBoat || IsDowned)
        {
            return false;
        }

        LeaveBoat(landingPosition);
        return true;
    }

    public void SetDowned(bool value)
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
        bool fireRequested = _fireRequested;
        bool abilityRequested = _abilityRequested;

        ClearActionRequests();

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

        _rb.isKinematic = _savedIsKinematic;
        _rb.detectCollisions = _savedDetectCollisions;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _hfsm.SetOnBoat(false);
        RefreshState();

        Disembarked?.Invoke(previousBoat);
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
        _fireRequested = false;
        _abilityRequested = false;
    }
}
