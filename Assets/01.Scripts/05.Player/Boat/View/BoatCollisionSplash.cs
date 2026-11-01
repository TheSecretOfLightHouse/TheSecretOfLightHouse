using Mirror;
using System.Collections.Generic;
using Lighthouse.Systems.Audio;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.World.Query.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatController), typeof(Rigidbody))]
    public sealed class BoatCollisionSplash : NetworkBehaviour
    {
        [SerializeField] private BoatCollisionSplashEffect _effectPrefab;
        [SerializeField] private AudioClip _boatImpactClip;
        [SerializeField, Range(0f, 1f)] private float _boatImpactVolume = 1f;
        [Tooltip("Offset in world metres from the contact: X along the surface, Y up, Z away from the obstacle.")]
        [SerializeField] private Vector3 _effectOffset = Vector3.zero;
        [SerializeField, Min(0.1f)] private float _effectScale = 1.5f;
        [SerializeField] private bool _useWaterSurface = true;
        [SerializeField] private float _waterSurfaceOffset = 0.04f;
        [SerializeField, Min(0.01f)] private float _minimumSpeed = 0.35f;
        [SerializeField, Min(0.01f)] private float _fullStrengthSpeed = 8f;
        [SerializeField, Min(0.01f)] private float _cooldown = 0.18f;
        [SerializeField, Min(0f)] private float _contactAllowance = 2f;

        private BoatController _boat;
        private Rigidbody _body;
        private Collider _hull;
        private BoatCannonFeedback _feedback;
        private float _nextLocalTime;
        private double _nextServerTime;
        private readonly Dictionary<uint, double> _nextBoatImpactTimes = new Dictionary<uint, double>();

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _body = GetComponent<Rigidbody>();
            _hull = GetComponent<Collider>();
            _feedback = GetComponent<BoatCannonFeedback>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0)
            {
                return;
            }

            Vector3 position = Vector3.zero;
            Vector3 normal = Vector3.zero;
            int count = 0;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 horizontal = Vector3.ProjectOnPlane(contact.normal, Vector3.up);
                if (horizontal.sqrMagnitude < 0.1f)
                {
                    continue;
                }
                position += contact.point;
                normal += horizontal;
                count++;
            }
            if (count > 0)
            {
                TryPlaySplash(collision.collider, position / count, normal / count,
                    collision.relativeVelocity.magnitude);
            }
        }

        private bool TryPlaySplash(Collider other, Vector3 position, Vector3 contactNormal, float speed)
        {
            if (!_effectPrefab || !_boat.CanSimulateLocalPhysics || _boat.IsWrecked
                || Time.time < _nextLocalTime)
            {
                return false;
            }

            if (other.isTrigger || other.attachedRigidbody == _body
                || other.GetComponentInParent<PlayerController>())
            {
                return false;
            }

            Vector3 normal = Vector3.ProjectOnPlane(contactNormal, Vector3.up);
            if (normal.sqrMagnitude < 0.1f || speed < _minimumSpeed)
            {
                return false;
            }

            _nextLocalTime = Time.time + _cooldown;
            normal.Normalize();
            if (_hull)
            {
                position = _hull.ClosestPoint(position);
            }
            PlayerController owner = _boat.Owner;
            Vector3 target = owner ? owner.transform.position : _body.worldCenterOfMass;
            Vector3 towardBoat = Vector3.ProjectOnPlane(target - position, Vector3.up);
            if (towardBoat.sqrMagnitude > 0.01f)
            {
                normal = towardBoat.normalized;
            }
            float strength = Mathf.InverseLerp(_minimumSpeed, _fullStrengthSpeed, speed);
            bool rockHull = other.GetComponentInParent<MonsterBase>();
            PlaySplash(position, normal, strength, rockHull);
            if (NetworkClient.active && isClient && isOwned)
            {
                BoatController otherBoat = other.GetComponentInParent<BoatController>();
                CmdReportSplash(position, normal, strength, rockHull, otherBoat ? otherBoat.netId : 0);
            }
            else if (!NetworkClient.active && !NetworkServer.active
                && other.GetComponentInParent<BoatController>())
            {
                PlayBoatImpact(position);
            }
            return true;
        }

        [Command]
        private void CmdReportSplash(Vector3 position, Vector3 normal, float strength, bool rockHull, uint otherBoatId,
            NetworkConnectionToClient sender = null)
        {
            if (!CanRelaySplash(position, normal, strength, sender))
            {
                return;
            }

            _nextServerTime = NetworkTime.time + _cooldown;
            RpcPlaySplash(position, Vector3.ProjectOnPlane(normal, Vector3.up).normalized,
                Mathf.Clamp01(strength), rockHull);
            RelayBoatImpact(otherBoatId, position);
        }

        private void RelayBoatImpact(uint otherBoatId, Vector3 position)
        {
            if (otherBoatId == netId || !NetworkServer.spawned.TryGetValue(otherBoatId, out NetworkIdentity identity)
                || !identity.TryGetComponent(out BoatCollisionSplash other) || !other._hull
                || other._boat.IsWrecked
                || other._hull.bounds.SqrDistance(position) > _contactAllowance * _contactAllowance)
            {
                return;
            }
            double now = NetworkTime.time;
            if (_nextBoatImpactTimes.TryGetValue(otherBoatId, out double nextTime) && now < nextTime)
            {
                return;
            }
            // Both owners report the same contact; reserve the pair on both boats before broadcasting.
            double next = now + _cooldown;
            _nextBoatImpactTimes[otherBoatId] = next;
            other._nextBoatImpactTimes[netId] = next;
            RpcPlayBoatImpact(position);
        }

        [ClientRpc]
        private void RpcPlayBoatImpact(Vector3 position)
        {
            PlayBoatImpact(position);
        }

        private void PlayBoatImpact(Vector3 position)
        {
            if (_boatImpactClip && SoundManager.Instance)
            {
                SoundManager.Instance.PlayEffectAt(_boatImpactClip, position, _boatImpactVolume);
            }
        }

        private bool CanRelaySplash(Vector3 position, Vector3 normal, float strength,
            NetworkConnectionToClient sender)
        {
            PlayerController owner = _boat.Owner;
            if (sender == null || !owner || owner.connectionToClient != sender
                || sender.identity != owner.netIdentity || !_boat.isActiveAndEnabled
                || !_boat.IsOccupied || owner.CurrentBoat != _boat || owner.IsDowned
                || _boat.IsWrecked || NetworkTime.time < _nextServerTime
                || !IsFinite(position) || !IsFinite(normal) || !float.IsFinite(strength)
                || normal.sqrMagnitude > 1.1f
                || Vector3.ProjectOnPlane(normal, Vector3.up).sqrMagnitude < 0.1f)
            {
                return false;
            }

            // Client-authoritative movement can arrive slightly after the one-shot contact report.
            return _hull && _hull.bounds.SqrDistance(position) <= _contactAllowance * _contactAllowance;
        }

        [ClientRpc(includeOwner = false)]
        private void RpcPlaySplash(Vector3 position, Vector3 normal, float strength, bool rockHull)
        {
            PlaySplash(position, normal, strength, rockHull);
        }

        private void PlaySplash(Vector3 position, Vector3 normal, float strength, bool rockHull)
        {
            if (rockHull && _feedback)
            {
                _feedback.PlayCollisionRock(normal, strength);
            }
            if (!_effectPrefab)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            // Physics contacts can lie above the waves; visual water starts at the waterline.
            if (_useWaterSurface && WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
            {
                float height = world.SampleWaterHeight(position);
                if (float.IsFinite(height))
                {
                    position.y = height + _waterSurfaceOffset;
                }
            }
            Vector3 effectPosition = position + rotation * _effectOffset;
            BoatCollisionSplashEffect effect = Instantiate(_effectPrefab, effectPosition, rotation);
            effect.transform.localScale *= Mathf.Max(0.1f, _effectScale);
            SceneManager.MoveGameObjectToScene(effect.gameObject, gameObject.scene);
            effect.Play(strength);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
