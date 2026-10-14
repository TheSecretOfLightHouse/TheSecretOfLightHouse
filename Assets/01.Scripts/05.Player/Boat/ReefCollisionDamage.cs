using Mirror;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class ReefCollisionDamage : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _damage = 25f;
    [SerializeField, Min(0f)]
    private float _contactMargin = 0.05f;
    [SerializeField]
    private LayerMask _boatLayers = ~0;

    private readonly Collider[] _hits = new Collider[64];
    private HashSet<BoatDurability> _previousContacts =
        new HashSet<BoatDurability>();
    private HashSet<BoatDurability> _currentContacts =
        new HashSet<BoatDurability>();

    private BoxCollider _reefCollider;

    private void Awake()
    {
        _reefCollider = GetComponent<BoxCollider>();
    }

    private void FixedUpdate()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        _currentContacts.Clear();

        if (_reefCollider.enabled && !_reefCollider.isTrigger)
        {
            CheckBoatContacts();
        }

        HashSet<BoatDurability> previous = _previousContacts;
        _previousContacts = _currentContacts;
        _currentContacts = previous;
    }

    private void OnDisable()
    {
        _previousContacts.Clear();
        _currentContacts.Clear();
    }

    private void CheckBoatContacts()
    {
        Transform reefTransform = _reefCollider.transform;
        Vector3 scale = reefTransform.lossyScale;
        scale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));

        Vector3 center =
            reefTransform.TransformPoint(_reefCollider.center);
        Vector3 halfExtents =
            Vector3.Scale(_reefCollider.size * 0.5f, scale);

        halfExtents += Vector3.one * _contactMargin;

        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _hits,
            reefTransform.rotation,
            _boatLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i];
            Rigidbody body = hit.attachedRigidbody;

            if (!body|| !body.TryGetComponent(out BoatController boat) || !boat.isServer || !boat.TryGetComponent(out BoatDurability durability) || durability.IsWrecked)
            {
                continue;
            }

            if (!_currentContacts.Add(durability))
            {
                continue;
            }

            if (!_previousContacts.Contains(durability))
            {
                durability.ApplyDamage(_damage);
            }
        }
    }
}