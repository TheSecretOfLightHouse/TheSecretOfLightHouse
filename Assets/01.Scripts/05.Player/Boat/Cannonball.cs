using Lighthouse.Monsters.AI.Net;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.View;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class Cannonball : NetworkBehaviour
{
    [SerializeField, Min(0.01f)]
    private float _lifetime = 5f;

    private Rigidbody _rb;
    private SphereCollider _collider;
    private float _damage;
    private double _expireTime;
    private bool _hasHit;
    private Vector3 _previousPosition;
    private bool _hasWaterSample;
    private float _previousWaterOffset;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<SphereCollider>();
    }

    [Server]
    public void Initialize(Vector3 velocity, float damage, Collider[] boatColliders, Collider[] playerColliders)
    {
        _damage = damage;

        IgnoreCollisions(boatColliders);
        IgnoreCollisions(playerColliders);

        _rb.isKinematic = false;
        _rb.linearVelocity = velocity;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _expireTime = NetworkTime.time + _lifetime;
        _previousPosition = transform.position;
        if (WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
        {
            float height = world.SampleWaterHeight(_previousPosition);
            _hasWaterSample = !float.IsNaN(height) && !float.IsInfinity(height);
            _previousWaterOffset = _previousPosition.y - height;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer)
        {
            _rb.isKinematic = true;
            _collider.enabled = false;
        }
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (NetworkTime.time >= _expireTime)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!isServer || _hasHit || !WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
        {
            return;
        }

        Vector3 position = _rb.position;
        float height = world.SampleWaterHeight(position);
        float offset = position.y - height;
        if (float.IsNaN(height) || float.IsInfinity(height))
        {
            _hasWaterSample = false;
            return;
        }
        if (_hasWaterSample && _previousWaterOffset > 0f && offset <= 0f)
        {
            _hasHit = true;
            float fraction = _previousWaterOffset / (_previousWaterOffset - offset);
            Vector3 impact = Vector3.Lerp(_previousPosition, position, fraction);
            RpcWaterImpact(impact);
            NetworkServer.Destroy(gameObject);
            return;
        }

        _hasWaterSample = true;
        _previousWaterOffset = offset;
        _previousPosition = position;
    }

    [ClientRpc]
    private void RpcWaterImpact(Vector3 position)
    {
        WorldAudioView.PlayCannonWaterImpact(position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer || _hasHit)
        {
            return;
        }

        _hasHit = true;

        try
        {
            SeaMonster monster = collision.collider.GetComponentInParent<SeaMonster>();

            if (monster)
            {
                monster.TryApplyDamage(_damage);
            }
        }
        finally
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void IgnoreCollisions(Collider[] colliders)
    {
        foreach (Collider other in colliders)
        {
            if (other)
            {
                Physics.IgnoreCollision(_collider, other);
            }
        }
    }
}
