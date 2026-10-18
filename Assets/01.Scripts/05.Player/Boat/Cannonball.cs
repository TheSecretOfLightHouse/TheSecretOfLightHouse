using Lighthouse.Monsters.AI.Net;
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