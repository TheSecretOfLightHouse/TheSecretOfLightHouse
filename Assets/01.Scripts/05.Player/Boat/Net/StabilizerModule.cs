using Lighthouse.World.Ocean.View;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StabilizerModule : BoatModule
{
    [SerializeField, Min(0f)]
    private float _angularDamping = 2f;

    [SerializeField, Min(0f)]
    private float _maxAngularAcceleration = 5f;

    [SerializeField, Range(0f, 1f)]
    private float _waveTiltReduction = 0.3f;

    [SerializeField, Range(0f, 1f)]
    private float _collisionTiltReduction = 0.5f;

    [SerializeField, Min(1f)]
    private float _collisionDampingMultiplier = 1.5f;

    [SerializeField, Range(0f, 1f)]
    private float _collisionPushReduction = 0.5f;

    private BoatController _boat;
    private Rigidbody _body;
    private WaveTiltVisual _waveTilt;

    public override BoatModuleType ModuleType => BoatModuleType.Stabilizer;

    public bool IsVisualStabilizing => isActiveAndEnabled && isClient && IsApplied
        && _boat && _boat.isActiveAndEnabled && _boat.IsOccupied && !_boat.IsWrecked;

    public float CollisionTiltMultiplier => IsVisualStabilizing
        ? 1f - Mathf.Clamp01(_collisionTiltReduction) : 1f;

    public float CollisionDampingMultiplier => IsVisualStabilizing
        ? Mathf.Max(1f, _collisionDampingMultiplier) : 1f;

    public float CollisionPushMultiplier => IsVisualStabilizing
        ? 1f - Mathf.Clamp01(_collisionPushReduction) : 1f;

    public bool CanStabilize
    {
        get
        {
            if (!isClient || !isOwned || !IsApplied || !_boat || !_body || _body.isKinematic)
            {
                return false;
            }

            PlayerController player = _boat.Owner;

            return player
                && player.isActiveAndEnabled
                && player.CanProcessBoatInput
                && !player.IsDowned
                && player.IsOnBoat
                && player.CurrentBoat == _boat
                && _boat.IsOccupied
                && !_boat.IsWrecked;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        _boat = GetComponent<BoatController>();
        _body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // WaterBody adds this dynamically; retry after destruction/replacement as well as at startup.
        if (!_waveTilt)
        {
            TryGetComponent(out _waveTilt);
        }

        if (!_waveTilt)
        {
            return;
        }

        // Observers use the same replicated equipment and occupancy, not local input authority.
        if (IsVisualStabilizing && _waveTilt.isActiveAndEnabled)
        {
            _waveTilt.TrySetTiltMultiplier(this, 1f - Mathf.Clamp01(_waveTiltReduction));
        }
        else
        {
            _waveTilt.ReleaseTiltMultiplier(this);
        }
    }

    protected override void OnDisable()
    {
        ReleaseWaveTilt();
        base.OnDisable();
    }

    private void FixedUpdate()
    {
        if (!CanStabilize)
        {
            return;
        }

        Vector3 tiltVelocity = Vector3.ProjectOnPlane(_body.angularVelocity, Vector3.up);

        Vector3 correction = -tiltVelocity * _angularDamping;

        correction = Vector3.ClampMagnitude(correction,_maxAngularAcceleration);

        _body.AddTorque(correction, ForceMode.Acceleration);
    }

    protected override void ApplyEffect()
    {
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer equipped.",
            this);
    }

    protected override void RemoveEffect()
    {
        ReleaseWaveTilt();
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer effect removed.",
            this);
    }

    private void ReleaseWaveTilt()
    {
        if (_waveTilt)
        {
            _waveTilt.ReleaseTiltMultiplier(this);
        }
    }
}
