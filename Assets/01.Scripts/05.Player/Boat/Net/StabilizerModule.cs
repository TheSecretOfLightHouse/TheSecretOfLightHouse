using UnityEngine;

[DisallowMultipleComponent]
public sealed class StabilizerModule : BoatModule
{
    [SerializeField, Min(0f)]
    private float _angularDamping = 2f;

    [SerializeField, Min(0f)]
    private float _maxAngularAcceleration = 5f;

    private BoatController _boat;
    private Rigidbody _body;

    public override BoatModuleType ModuleType => BoatModuleType.Stabilizer;

    public bool CanStabilize
    {
        get
        {
            if (!isClient || !isOwned || !IsApplied
                || !_boat || !_body || _body.isKinematic)
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

    protected override void ApplyEffect()
    {
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer equipped.",
            this);
    }

    protected override void RemoveEffect()
    {
        Debug.Log(
            $"[{nameof(StabilizerModule)}] Stabilizer effect removed.",
            this);
    }
}
