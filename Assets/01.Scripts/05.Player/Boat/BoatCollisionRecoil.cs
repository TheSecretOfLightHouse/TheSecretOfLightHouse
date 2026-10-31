using Mirror;
using Lighthouse.Monsters.AI.Net;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(Rigidbody))]
public sealed class BoatCollisionRecoil : MonoBehaviour
{
    private const float DirectionEpsilon = 0.0001f;

    [SerializeField, Min(0f)]
    private float _propulsionPauseDuration = 0.3f;

    private readonly Dictionary<BoatCollisionRecoilSource, float> _nextRecoilTimes = new Dictionary<BoatCollisionRecoilSource, float>();

    private BoatController _boat;
    private Rigidbody _rb;
    private StabilizerModule _stabilizer;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _rb = GetComponent<Rigidbody>();
        _stabilizer = GetComponent<StabilizerModule>();
    }

    private void OnDisable()
    {
        _nextRecoilTimes.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!CanApplyRecoil() || collision.contactCount == 0)
        {
            return;
        }

        float pushMultiplier = GetPushMultiplier(collision.collider);
        ReduceContactPush(collision, pushMultiplier);

        BoatCollisionRecoilSource source = collision.collider.GetComponentInParent<BoatCollisionRecoilSource>();

        if (!source || !source.isActiveAndEnabled || source.RecoilSpeed <= 0f)
        {
            return;
        }

        if (_nextRecoilTimes.TryGetValue(source, out float nextTime)  && Time.time < nextTime)
        {
            return;
        }

        Vector3 direction = collision.GetContact(0).normal;
        direction.y = 0f;

        if (direction.sqrMagnitude < DirectionEpsilon)
        {
            return;
        }

        direction.Normalize();
        _nextRecoilTimes[source] = Time.time + source.Cooldown;

        ApplyRecoil(direction, source.RecoilSpeed, pushMultiplier);

        PlayerController owner = _boat.Owner;
        if (_boat.IsOccupied && owner && owner.CurrentBoat == _boat && !owner.IsDowned
            && owner.TryGetComponent(out PlayerActionEvents actionEvents))
        {
            actionEvents.Publish(PlayerAnimationAction.Stumble);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (CanApplyRecoil() && collision.contactCount > 0)
        {
            ReduceContactPush(collision, GetPushMultiplier(collision.collider));
        }
    }

    private float GetPushMultiplier(Collider other)
    {
        return _stabilizer && other.GetComponentInParent<MonsterBase>()
            ? _stabilizer.CollisionPushMultiplier : 1f;
    }

    private void ReduceContactPush(Collision collision, float multiplier)
    {
        if (multiplier >= 1f)
        {
            return;
        }

        Vector3 direction = Vector3.ProjectOnPlane(collision.GetContact(0).normal, Vector3.up);
        if (direction.sqrMagnitude < DirectionEpsilon)
        {
            return;
        }

        direction.Normalize();
        // Cancel only the outward velocity added by this contact, preserving propulsion and buoyancy.
        // Unity flips contact normals for the recipient, but keeps the pair impulse unchanged.
        float outwardImpulse = Mathf.Abs(Vector3.Dot(collision.impulse, direction));
        _rb.linearVelocity -= direction * (outwardImpulse / _rb.mass * (1f - multiplier));
    }

    private bool CanApplyRecoil()
    {
        if (!_boat || !_rb || _rb.isKinematic || _boat.IsWrecked)
        {
            return false;
        }

        bool isNetworkActive = NetworkClient.active || NetworkServer.active;

        return !isNetworkActive || (_boat.isClient && _boat.isOwned);
    }

    private void ApplyRecoil(Vector3 direction, float speed, float pushMultiplier)
    {
        Vector3 velocity = _rb.linearVelocity;
        float inwardSpeed = Vector3.Dot(velocity, direction);

        if (inwardSpeed < 0f)
        {
            _rb.linearVelocity = velocity - direction * inwardSpeed;
        }

        _rb.AddForce(direction * (speed * pushMultiplier), ForceMode.VelocityChange);
        _boat.PreserveCollisionRecoil(_propulsionPauseDuration * pushMultiplier);
    }
}
