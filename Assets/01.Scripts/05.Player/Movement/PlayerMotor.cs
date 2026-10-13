using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    [SerializeField]
    private float _speed = 5f;

    [SerializeField]
    private float _originAcceleration = 10f;

    [SerializeField, Min(0f)]
    private float _jumpSpeed = 5f;

    [SerializeField, Min(0f)]
    private float _groundCheckDistance = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float _minimumGroundNormalY = 0.65f;

    [SerializeField]
    private LayerMask _groundLayers = ~0;

    private Collider _bodyCollider;
    private readonly RaycastHit[] _groundHits = new RaycastHit[16];

    public bool IsJumping { get; private set; }
    private void Awake()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }

        _bodyCollider = GetComponent<Collider>();
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
        _rb.MoveRotation(rotation);
    }

    public bool TryJump()
    {
        if (_rb.isKinematic || !_rb.detectCollisions
            || IsJumping || _jumpSpeed <= 0f
            || _rb.linearVelocity.y > 0f || !CheckGrounded())
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
        if(config.InputLocked)
        {
            direction = Vector3.zero;
        }

        direction.y = 0f;
        Vector3 targetVelocity = Vector3.ClampMagnitude(direction, 1f) * _speed * config.SpeedMultiplier;
        float acceleration = _originAcceleration * config.AccelerationMultiplier;
        Vector3 velocity = _rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * fixedDt);
        _rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
    }

    private void SetJumping(bool value)
    {
        if (IsJumping == value)
        {
            return;
        }

        IsJumping = value;
    }

    private bool CheckGrounded()
    {
        if (!_bodyCollider || !_bodyCollider.enabled)
        {
            return false;
        }

        Bounds bounds = _bodyCollider.bounds;

        int hitCount = Physics.RaycastNonAlloc(
            bounds.center,
            Vector3.down,
            _groundHits,
            bounds.extents.y + _groundCheckDistance,
            _groundLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundHits[i];

            if (hit.collider.attachedRigidbody == _rb
                || hit.collider.transform.IsChildOf(transform)
                || hit.normal.y < _minimumGroundNormalY)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
