using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ResourcePickup : NetworkBehaviour
{
    private static readonly List<ResourcePickup> ActivePickups = new List<ResourcePickup>();

    [SyncVar]
    private int _resourceId;
    [SyncVar]
    private int _amount;
    private bool _isInitialized;
    private bool _isTaken;

    private bool _isCollectionLocked;

    [SyncVar] private bool _hasHarvestFlight;
    [SyncVar] private Vector3 _flightStart;
    [SyncVar] private Vector3 _flightEnd;
    [SyncVar] private double _flightStartedAt;
    [SyncVar] private float _flightDuration;
    [SyncVar] private float _flightHeight;

    private bool _isFlightSettled;
    private Rigidbody _body;
    private bool _wasKinematic;
    private Collider _collider;

    internal static IReadOnlyList<ResourcePickup> Active => ActivePickups;
    internal Vector3 RestingPosition => _hasHarvestFlight ? _flightEnd : transform.position;
    internal float FootprintRadius => _collider
        ? Mathf.Max(_collider.bounds.extents.x, _collider.bounds.extents.z, 0.2f)
        : 0.3f;

    public int ResourceId => _resourceId;
    public int Amount => _amount;

    public event Action<ResourcePickup> RecycleRequested;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _wasKinematic = _body && _body.isKinematic;
    }

    private void OnEnable()
    {
        ActivePickups.Add(this);
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    private void Update()
    {
        if (!_hasHarvestFlight || _isFlightSettled)
        {
            return;
        }

        float progress = Mathf.Clamp01((float)((NetworkTime.time - _flightStartedAt) / _flightDuration));
        Vector3 position = Vector3.Lerp(_flightStart, _flightEnd, progress);
        position.y += 4f * _flightHeight * progress * (1f - progress);
        transform.position = position;
        _isFlightSettled = progress >= 1f;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _isFlightSettled = false;
        if (_hasHarvestFlight && _body)
        {
            _body.isKinematic = true;
        }

        Update();
    }

    [Server]
    public void BeginHarvestFlight(Vector3 start, Vector3 end, float duration, float height)
    {
        _flightStart = start;
        _flightEnd = end;
        _flightDuration = Mathf.Max(0.1f, duration);
        _flightHeight = Mathf.Max(0f, height);
        _flightStartedAt = NetworkTime.time;
        _hasHarvestFlight = true;
        _isFlightSettled = false;
        if (_body)
        {
            _body.isKinematic = true;
        }
    }

    [Server]
    public bool TryResetForReuse()
    {
        if (netId != 0)
        {
            return false;
        }

        _resourceId = 0;
        _amount = 0;
        _isInitialized = false;
        _isTaken = false;
        _isCollectionLocked = false;
        _hasHarvestFlight = false;
        _isFlightSettled = false;
        if (_body)
        {
            _body.isKinematic = _wasKinematic;
        }

        return true;
    }

    [Server]
    public bool TryInitialize(int resourceId, int amount, bool isCollectionLocked = false)
    {
        if (_isInitialized || amount <= 0)
        {
            return false;
        }

        _resourceId = resourceId;
        _amount = amount;
        _isCollectionLocked = isCollectionLocked;
        _isInitialized = true;
        return true;
    }

    [Server]
    public void UnlockCollection()
    {
        _isCollectionLocked = false;
    }

    [Server]
    public bool TryCollect(PlayerInventory inventory)
    {
        if (!_isInitialized || _isTaken || _isCollectionLocked || !inventory
            || (_hasHarvestFlight && NetworkTime.time < _flightStartedAt + _flightDuration))
        {
            return false;
        }

        _isTaken = true;
        if (!inventory.TryAdd(_resourceId, _amount))
        {
            _isTaken = false;
            return false;
        }

        if (RecycleRequested != null)
        {
            RecycleRequested.Invoke(this);
        }
        else
        {
            NetworkServer.Destroy(gameObject);
        }

        return true;
    }
}
