using UnityEngine;
using Lighthouse.World.Ocean.View;

[DisallowMultipleComponent]
public sealed class StabilizerModule : BoatModule
{
    [SerializeField, Min(0f)]
    private float _angularDamping = 2f;

    [SerializeField, Min(0f)]
    private float _maxAngularAcceleration = 5f;

    private BoatController _boat;
    private Rigidbody _body;
    // Initial tuning value; this affects the wave visual channel only.
    [SerializeField, Range(0f, 1f)] private float _waveTiltMultiplier = 0.7f;
    private WaveTiltVisual _waveTilt;

    public override BoatModuleType ModuleType => BoatModuleType.Stabilizer;

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

    private void Update()
    {
        // WaterBody adds this dynamically; retry after destruction/replacement as well as at startup.
        if (!_waveTilt) TryGetComponent(out _waveTilt);
        bool show = isActiveAndEnabled && isClient && IsApplied && _waveTilt && _waveTilt.isActiveAndEnabled && _boat
            && _boat.isActiveAndEnabled && _boat.IsOccupied && !_boat.IsWrecked;
        PlayerController player = _boat ? _boat.Owner : null;
        show &= player && player.isActiveAndEnabled && !player.IsDowned
            && player.IsOnBoat && player.CurrentBoat == _boat;
        if (_waveTilt)
        {
            if (show) _waveTilt.TrySetTiltMultiplier(this, _waveTiltMultiplier);
            else _waveTilt.ReleaseTiltMultiplier(this);
        }
    }

    protected override void OnDisable()
    {
        if (_waveTilt) _waveTilt.ReleaseTiltMultiplier(this);
        base.OnDisable();
    }

    protected override void ApplyEffect()
    {
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer equipped.",
            this);
    }

    protected override void RemoveEffect()
    {
        if (_waveTilt) _waveTilt.ReleaseTiltMultiplier(this);
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer effect removed.",
            this);
    }
}
