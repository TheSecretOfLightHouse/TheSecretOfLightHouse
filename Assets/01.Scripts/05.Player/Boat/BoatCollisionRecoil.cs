using Mirror;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(Rigidbody))]
public sealed class BoatCollisionRecoil : MonoBehaviour
{
    private const float DirectionEpsilon = 0.0001f;

    private readonly Dictionary<BoatCollisionRecoilSource, float> _nextRecoilTimes = new Dictionary<BoatCollisionRecoilSource, float>();

    private BoatController _boat;
    private Rigidbody _rb;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _rb = GetComponent<Rigidbody>();
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

        ApplyRecoil(direction, source.RecoilSpeed);
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

    private void ApplyRecoil(Vector3 direction, float speed)
    {
        Vector3 velocity = _rb.linearVelocity;
        float inwardSpeed = Vector3.Dot(velocity, direction);

        if (inwardSpeed < 0f)
        {
            _rb.linearVelocity = velocity - direction * inwardSpeed;
        }

        _rb.AddForce(direction * speed, ForceMode.VelocityChange);
    }
}
