using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    [SerializeField]
    private float _speed = 3f;

    [SerializeField]
    private float _originAcceleration = 10f;

    [SerializeField, Min(0f)]
    private float _groundAcceleration = 30f;

    [SerializeField, Min(0f)]
    private float _stopDeceleration = 60f;

    [SerializeField, Min(0f)]
    private float _turnAcceleration = 60f;

    [SerializeField, Range(0f, 180f)]
    private float _turnAngle = 15f;

    [SerializeField, Min(0f)]
    private float _jumpSpeed = 5f;

    [SerializeField, Min(0f)]
    private float _groundCheckDistance = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float _minimumGroundNormalY = 0.65f;

    [SerializeField, Min(0f)]
    private float _groundStickSpeed = 1.5f;

    [SerializeField]
    private LayerMask _groundLayers = ~0;

    [SerializeField, Min(0.1f)]
    private float _shoreClimbReach = 0.6f;

    [SerializeField, Min(0.1f)]
    private float _shoreClimbHeight = 0.8f;

    [SerializeField, Min(0.1f)]
    private float _shoreClimbSpeed = 2f;

    [SerializeField, Min(0f)]
    private float _shoreWaterClearance = 0.1f;

    private readonly Collider[] _shoreOverlaps = new Collider[16];
    private readonly RaycastHit[] _shoreHits = new RaycastHit[16];
    private Collider _bodyCollider;
    private CapsuleCollider _capsuleCollider;
    private PhysicsScene _physicsScene;
    private readonly RaycastHit[] _groundHits = new RaycastHit[16];

    public float ShoreClimbReach => _shoreClimbReach
        + (_capsuleCollider ? _capsuleCollider.bounds.extents.x : 0f);

    public bool IsJumping { get; private set; }
    public bool IsOffGround => _rb && !_rb.isKinematic && _rb.detectCollisions
        && !CheckGrounded();
    private void Awake()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }

        _bodyCollider = GetComponent<Collider>();
        _capsuleCollider = _bodyCollider as CapsuleCollider;
        _physicsScene = gameObject.scene.GetPhysicsScene();
    }

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!IsJumping)
        {
            return;
        }

        if (_rb.isKinematic || !_rb.detectCollisions)
        {
            SetJumping(false);
            return;
        }

        if (_rb.linearVelocity.y <= 0f && CheckGrounded())
        {
            SetJumping(false);
        }
    }

    private void OnDisable()
    {
        SetJumping(false);
    }

    public void Face(Quaternion rotation)
    {
        if (_rb.isKinematic)
        {
            return;
        }

        // Facing is input-driven; collision spin must not survive after input stops.
        Vector3 angularVelocity = _rb.angularVelocity;
        angularVelocity.y = 0f;
        _rb.angularVelocity = angularVelocity;
        _rb.MoveRotation(rotation);
    }

    public bool TryJump()
    {
        if (_rb.isKinematic || !_rb.detectCollisions
            || IsJumping || _jumpSpeed <= 0f || !CheckGrounded())
        {
            return false;
        }

        Vector3 velocity = _rb.linearVelocity;
        velocity.y = _jumpSpeed;
        _rb.linearVelocity = velocity;

        SetJumping(true);
        return true;
    }

    public void Move(Vector3 direction, MoveConfig config, float fixedDt)
    {
        if (_rb.isKinematic || !_rb.detectCollisions)
        {
            return;
        }

        if (config.InputLocked)
        {
            direction = Vector3.zero;
        }

        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);

        float speed = _speed * config.SpeedMultiplier;
        float acceleration = _originAcceleration * config.AccelerationMultiplier;
        Vector3 velocity = _rb.linearVelocity;

        if (!IsJumping && TryGetGroundNormal(
            out Vector3 groundNormal, out float groundDistance))
        {
            float inputAmount = direction.magnitude;

            Vector3 slopeDirection =
                Vector3.ProjectOnPlane(direction, groundNormal).normalized;

            Vector3 targetVelocity = slopeDirection * speed * inputAmount;
            Vector3 groundVelocity =
                Vector3.ProjectOnPlane(velocity, groundNormal);

            float groundAcceleration = _groundAcceleration;

            if (targetVelocity.sqrMagnitude == 0f)
            {
                groundAcceleration = _stopDeceleration;
            }
            else if (groundVelocity.sqrMagnitude > 0f
                && Vector3.Angle(groundVelocity, targetVelocity) > _turnAngle)
            {
                groundAcceleration = _turnAcceleration;
            }

            Vector3 movementVelocity = Vector3.MoveTowards(
                groundVelocity,
                targetVelocity,
                groundAcceleration * config.AccelerationMultiplier * fixedDt);

            float stickSpeed = 0f;

            if (groundDistance > _bodyCollider.contactOffset
                && fixedDt > 0f)
            {
                stickSpeed = Mathf.Min(
                    _groundStickSpeed,
                    groundDistance * groundNormal.y / fixedDt);
            }

            float normalSpeed = Mathf.Min(
                Vector3.Dot(velocity, groundNormal), -stickSpeed);

            _rb.linearVelocity = movementVelocity + groundNormal * normalSpeed;

            return;
        }

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 airTargetVelocity = direction * speed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            airTargetVelocity,
            acceleration * fixedDt);

        _rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            velocity.y,
            horizontalVelocity.z);
    }

    public bool CanStartShoreClimb(float waterHeight)
    {
        return _bodyCollider && !float.IsNaN(waterHeight) && !float.IsInfinity(waterHeight)
            && _bodyCollider.bounds.min.y <= waterHeight + _shoreWaterClearance
            && _bodyCollider.bounds.min.y >= waterHeight - _shoreClimbHeight;
    }

    public bool TryClimbShore(Vector3 direction, float groundHeight, float waterHeight, float fixedDt)
    {
        if (!_capsuleCollider || !_capsuleCollider.enabled || _capsuleCollider.direction != 1
            || !_rb || _rb.isKinematic || !_rb.detectCollisions || IsJumping
            || fixedDt <= 0f || direction.sqrMagnitude < 0.01f
            || !CanStartShoreClimb(waterHeight) || float.IsNaN(groundHeight) || float.IsInfinity(groundHeight))
        {
            return false;
        }

        Bounds bounds = _capsuleCollider.bounds;
        float skin = Mathf.Max(_capsuleCollider.contactOffset, 0.01f);
        float rise = groundHeight + skin * 2f - bounds.min.y;
        if (rise <= skin || rise > _shoreClimbHeight)
        {
            return false;
        }

        direction.y = 0f;
        direction.Normalize();
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) - skin;
        if (radius <= 0f)
        {
            return false;
        }

        Vector3 lower = new Vector3(bounds.center.x, bounds.min.y + radius, bounds.center.z);
        Vector3 upper = new Vector3(bounds.center.x, bounds.max.y - radius, bounds.center.z);
        Vector3 lift = Vector3.up * rise;
        int overlaps = _physicsScene.OverlapCapsule(
            lower + lift, upper + lift, radius, _shoreOverlaps,
            _groundLayers, QueryTriggerInteraction.Ignore);
        if (overlaps == _shoreOverlaps.Length)
        {
            return false;
        }

        for (int i = 0; i < overlaps; i++)
        {
            if (CanUseGround(_shoreOverlaps[i]))
            {
                return false;
            }
        }

        // Clearance includes the whole capsule, so shore assistance cannot tunnel through a dock or wall.
        if (IsShorePathBlocked(lower + lift, upper + lift, radius, direction, _shoreClimbReach)
            || IsShorePathBlocked(lower, upper, radius, Vector3.up, rise))
        {
            return false;
        }

        _rb.position += Vector3.up * Mathf.Min(rise, _shoreClimbSpeed * fixedDt);
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = Mathf.Max(0f, velocity.y);
        _rb.linearVelocity = velocity;
        return true;
    }

    private bool IsShorePathBlocked(Vector3 lower, Vector3 upper, float radius, Vector3 direction, float distance)
    {
        int hits = _physicsScene.CapsuleCast(lower, upper, radius, direction,
            _shoreHits, distance, _groundLayers, QueryTriggerInteraction.Ignore);
        if (hits == _shoreHits.Length)
        {
            return true;
        }

        for (int i = 0; i < hits; i++)
        {
            if (CanUseGround(_shoreHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private void SetJumping(bool value)
    {
        if (IsJumping == value)
        {
            return;
        }

        IsJumping = value;
    }

    public bool TryGetGroundCollider(out Collider groundCollider)
    {
        return TryGetGroundNormal(out _, out _, out groundCollider);
    }

    private bool CheckGrounded()
    {
        return TryGetGroundNormal(out _);
    }

    private bool TryGetGroundNormal(out Vector3 groundNormal)
    {
        return TryGetGroundNormal(out groundNormal, out _);
    }

    private bool TryGetGroundNormal(
        out Vector3 groundNormal, out float groundDistance)
    {
        return TryGetGroundNormal(out groundNormal, out groundDistance, out _);
    }

    private bool TryGetGroundNormal(
        out Vector3 groundNormal, out float groundDistance, out Collider groundCollider)
    {
        groundNormal = Vector3.up;
        groundDistance = 0f;
        groundCollider = null;

        if (!_bodyCollider || !_bodyCollider.enabled)
        {
            return false;
        }

        Bounds bounds = _bodyCollider.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);

        if (_capsuleCollider && _capsuleCollider.direction == 1)
        {
            Vector3 scale = _capsuleCollider.transform.lossyScale;
            radius = _capsuleCollider.radius
                * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        float skin = Mathf.Max(_bodyCollider.contactOffset, 0.001f);
        float probeRadius = Mathf.Max(radius - skin, 0.001f);
        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + radius + skin,
            bounds.center.z);

        int hitCount = _physicsScene.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            _groundHits,
            _groundCheckDistance + skin * 2f,
            _groundLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        bool hasGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundHits[i];

            if (!CanUseGround(hit.collider)
                || hit.normal.y < _minimumGroundNormalY
                || hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            groundNormal = hit.normal;
            groundCollider = hit.collider;
            groundDistance = Mathf.Max(0f, hit.distance - skin * 2f);
            hasGround = true;
        }

        return hasGround;
    }

    private bool CanUseGround(Collider candidate)
    {
        return candidate && !candidate.isTrigger
            && candidate.attachedRigidbody != _rb
            && !candidate.transform.IsChildOf(transform)
            && (_groundLayers.value & (1 << candidate.gameObject.layer)) != 0
            && !Physics.GetIgnoreLayerCollision(gameObject.layer, candidate.gameObject.layer)
            && !Physics.GetIgnoreCollision(_bodyCollider, candidate);
    }
}
