using UnityEngine;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField]
    private PlayerInputReader _inputReader;

    [SerializeField]
    private PlayerMotor _motor;

    [SerializeField, Min(0.01f)]
    private float _dashDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float _mouseSensitivity = 0.15f;

    private PlayerHFSM _hfsm;
    private float _heading;

    [Header("State transition tests")]
    [SerializeField]
    private bool _testUsingAbility;

    [SerializeField]
    private bool _testDowned;

    private void Awake()
    {
        ResolveReferences();
        _hfsm = new PlayerHFSM(Mathf.Max(0.01f, _dashDuration));
    }

    private void OnEnable()
    {
        _heading = transform.eulerAngles.y;
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
    }

    private void Update()
    {
        _hfsm.SetDowned(_testDowned);
        if(_testDowned)
        {
            _testUsingAbility = false;
        }
        if(!_testDowned)
        {
            _heading = Mathf.Repeat(_heading + _inputReader.LookInput.x * _mouseSensitivity, 360f);
        }

        Vector3 moveDirection =ConvertMoveInput(_inputReader.MoveInput);

        _hfsm.SetInput(moveDirection,_inputReader.DashPressed);

        _hfsm.SetAbilityStatus(_testUsingAbility,new MoveConfig
        {
            SpeedMultiplier = 0f,
            AccelerationMultiplier = 1f,
            InputLocked = true
        });

        _hfsm.Tick(Time.deltaTime);
        _inputReader.ConsumeDash();
    }

    private void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;

        _hfsm.FixedTick(fixedDeltaTime);

        if(!_hfsm.IsDowned)
        {
            _motor.Face(Quaternion.Euler(0f, _heading, 0f));
        }
        _motor.Move(_hfsm.MoveDirection,_hfsm.CurrentMoveConfig,fixedDeltaTime);
    }

    private Vector3 ConvertMoveInput(Vector2 input)
    {
        return Quaternion.Euler(0f, _heading, 0f) * new Vector3(input.x, 0f, input.y);
    }
}
