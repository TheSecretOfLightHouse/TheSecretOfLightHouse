using System.Collections.Generic;
using System.Threading;
using Mirror;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class MonsterContactResponse : MonoBehaviour
    {
        [SerializeField] private bool _suppressPhysicalPush = true;

        private static readonly int[] EmptyIds = new int[0];
        private readonly Dictionary<NetworkIdentity, Collider[]> _targets = new Dictionary<NetworkIdentity, Collider[]>();
        private readonly List<int> _colliderIds = new List<int>();
        private int[] _targetColliderIds = EmptyIds;
        private CapsuleCollider _collider;
        private int _colliderId;
        private bool _originalModifiableContacts;

        private void Awake()
        {
            _collider = GetComponent<CapsuleCollider>();
            _colliderId = _collider.GetInstanceID();
            _originalModifiableContacts = _collider.hasModifiableContacts;
        }

        private void OnEnable()
        {
            _collider.hasModifiableContacts = true;
            RefreshTargets();
            Physics.ContactModifyEvent += HandleContacts;
            Physics.ContactModifyEventCCD += HandleContacts;
        }

        private void FixedUpdate()
        {
            RefreshTargets();
        }

        private void OnDisable()
        {
            Physics.ContactModifyEvent -= HandleContacts;
            Physics.ContactModifyEventCCD -= HandleContacts;
            Volatile.Write(ref _targetColliderIds, EmptyIds);
            _targets.Clear();
            if (_collider != null)
            {
                _collider.hasModifiableContacts = _originalModifiableContacts;
            }
        }

        private void RefreshTargets()
        {
            _colliderIds.Clear();
            if (_suppressPhysicalPush)
            {
                // Boats are simulated by their owning client, so suppress contacts on clients too.
                Dictionary<uint, NetworkIdentity> spawned = NetworkServer.active
                    ? NetworkServer.spawned : NetworkClient.spawned;
                foreach (NetworkIdentity identity in spawned.Values)
                {
                    if (identity == null)
                    {
                        continue;
                    }
                    if (!_targets.TryGetValue(identity, out Collider[] colliders))
                    {
                        bool isTarget = identity.TryGetComponent<BoatController>(out _)
                            || identity.TryGetComponent<PlayerController>(out _);
                        colliders = isTarget ? identity.GetComponentsInChildren<Collider>(true) : new Collider[0];
                        _targets.Add(identity, colliders);
                    }
                    foreach (Collider target in colliders)
                    {
                        if (target != null && target.enabled && !target.isTrigger && target.gameObject.activeInHierarchy)
                        {
                            _colliderIds.Add(target.GetInstanceID());
                        }
                    }
                }
            }

            int[] previous = Volatile.Read(ref _targetColliderIds);
            bool changed = previous.Length != _colliderIds.Count;
            for (int i = 0; !changed && i < previous.Length; i++)
            {
                changed = previous[i] != _colliderIds[i];
            }
            if (changed)
            {
                Volatile.Write(ref _targetColliderIds, _colliderIds.ToArray());
            }
        }

        private void HandleContacts(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            // Physics may invoke this on worker threads; only immutable ID snapshots are read here.
            int[] targets = Volatile.Read(ref _targetColliderIds);
            for (int i = 0; i < pairs.Length; i++)
            {
                ModifiableContactPair pair = pairs[i];
                int otherId;
                if (pair.colliderInstanceID == _colliderId)
                {
                    otherId = pair.otherColliderInstanceID;
                }
                else if (pair.otherColliderInstanceID == _colliderId)
                {
                    otherId = pair.colliderInstanceID;
                }
                else
                {
                    continue;
                }
                if (System.Array.IndexOf(targets, otherId) < 0)
                {
                    continue;
                }
                // Keep a solid contact for the monster, but make the target immovable for this pair.
                // Removing the entire impulse also lets the monster pass through its attack target.
                ModifiableMassProperties mass = pair.massProperties;
                if (pair.colliderInstanceID == _colliderId)
                {
                    mass.otherInverseMassScale = 0f;
                    mass.otherInverseInertiaScale = 0f;
                }
                else
                {
                    mass.inverseMassScale = 0f;
                    mass.inverseInertiaScale = 0f;
                }
                pair.massProperties = mass;
            }
        }
    }
}
