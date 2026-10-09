using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatController : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;
    [SerializeField]
    private PlayerController _owner;
    [SerializeField]
    private Transform _boardingAnchor;
    [SerializeField, Min(0f)]
    private float _speed = 8f;
    [SerializeField, Min(0f)]
    private float _acceleration = 4f;
    [SerializeField, Min(0.01f)]
    private float _reloadDuration = 2f;

    private bool _isOccupied;
    private float _nextFireTime;

    public PlayerController Owner => _owner;
    public bool IsOccupied => _isOccupied;

    public Transform BoardingAnchor => _boardingAnchor ? _boardingAnchor : transform;

    public event Action<PlayerController> CannonFired;

    private void Awake()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }
    }
    private void Reset()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }
    }
    public bool TrySetOwner(PlayerController player)
    {
        if (!player)
        {
            return false;
        }

        if (_owner == player)
        {
            return true;
        }

        if (_owner != null || _isOccupied)
        {
            return false;
        }

        _owner = player;
        return true;
    }

    public bool TryBoard(PlayerController player)
    {
        if (!isActiveAndEnabled|| !player|| !player.isActiveAndEnabled|| player != _owner|| player.IsDowned|| _isOccupied)
        {
            return false;
        }

        _isOccupied = true;
        return true;
    }

    public void Disembark(PlayerController player)
    {
        if (!player || player != _owner || !_isOccupied)
        {
            return;
        }

        _isOccupied = false;
        Stop();
    }

    public void Move(PlayerController player, Vector3 direction, Quaternion rotation,MoveConfig config, float fixedDeltaTime)
    {
        if (!CanControl(player))
        {
            return;
        }

        if (config.InputLocked)
        {
            direction = Vector3.zero;
        }

        direction.y = 0f;

        Vector3 targetVelocity = Vector3.ClampMagnitude(direction, 1f) * _speed * config.SpeedMultiplier;
        Vector3 velocity = _rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        horizontal = Vector3.MoveTowards(horizontal, targetVelocity, _acceleration * config.AccelerationMultiplier * fixedDeltaTime);

        _rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

        if (!config.InputLocked)
        {
            _rb.MoveRotation(rotation);
        }
    }

    public bool TryFire(PlayerController player)
    {
        if (!CanControl(player) || Time.time < _nextFireTime)
        {
            return false;
        }

        _nextFireTime = Time.time + _reloadDuration;
        CannonFired?.Invoke(player);
        return true;
    }

    public void Stop()
    {
        Vector3 velocity = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
        _rb.angularVelocity = Vector3.zero;
    }
    private bool CanControl(PlayerController player)
    {
        return isActiveAndEnabled
            && player != null
            && player.isActiveAndEnabled
            && player == _owner
            && _isOccupied
            && player.CurrentBoat == this
            && !player.IsDowned;
    }
}
