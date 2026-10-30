using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.World.Ocean;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BuoyancyObject), typeof(WaveDriftBody))]
    public sealed class MonsterWaterBody : MonoBehaviour, IShipBody
    {
        [SerializeField, Min(0f)] private float _defaultDepth;
        [SerializeField, Min(0.1f)] private float _transitionTimeout = 15f;
        [SerializeField, Min(0f)] private float _depthTolerance = 0.35f;
        [SerializeField] private Vector3[] _buoyancyPoints;

        private Collider[] _colliders;
        private MonsterBase _monster;
        private Rigidbody _body;
        private BuoyancyObject _buoyancy;
        private WaveDriftBody _drift;
        private float _centerY;
        private bool _scriptedMotion;

        public uint ShipId => _monster != null ? _monster.netId : 0;
        public Rigidbody Body => _body;
        public IReadOnlyList<Vector3> BuoyancyPoints => _buoyancyPoints;
        public Vector3 CenterOfMassOffset => Vector3.zero;
        public float HullExposure => 1f;
        public float RightingStrength => 0f;
        public float TransitionTimeout => _transitionTimeout;
        public BuoyancyObject Buoyancy => _buoyancy;
        public float CenterOffsetY => transform.TransformVector(new Vector3(0f, _centerY, 0f)).y;
        public bool HasValidReading => _buoyancy != null && _buoyancy.Reading.IsValid;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>();
            _monster = GetComponent<MonsterBase>();
            _body = GetComponent<Rigidbody>();
            _buoyancy = GetComponent<BuoyancyObject>();
            _drift = GetComponent<WaveDriftBody>();
            if (_buoyancyPoints == null || _buoyancyPoints.Length == 0)
            {
                enabled = false;
                return;
            }
            for (int i = 0; i < _buoyancyPoints.Length; i++)
            {
                _centerY += _buoyancyPoints[i].y;
            }
            _centerY /= _buoyancyPoints.Length;
            RestoreDefaultDepth();
        }

        private void FixedUpdate()
        {
            ApplySimulation();
        }

        private void OnDisable()
        {
            if (_buoyancy != null)
            {
                _buoyancy.IsSimulating = false;
                _buoyancy.InvalidateSamples();
            }
            if (_drift != null)
            {
                _drift.enabled = false;
            }
            if (_body != null)
            {
                _body.useGravity = false;
            }
            _scriptedMotion = false;
        }

        public void SetScriptedMotion(bool scripted)
        {
            _scriptedMotion = scripted;
            ApplySimulation();
        }

        public void RestoreDefaultDepth()
        {
            _buoyancy.SetTargetDepth(_defaultDepth);
        }

        public float DepthForRoot(float rootDepth)
        {
            return Mathf.Max(0.01f, rootDepth - CenterOffsetY);
        }

        public bool HasReachedDepth(float depth)
        {
            return HasValidReading && Mathf.Abs(_buoyancy.Reading.MeanDepth - depth) <= _depthTolerance
                && Mathf.Abs(_body.linearVelocity.y) <= 1f;
        }

        public bool IsFullySubmerged(float clearance)
        {
            if (!HasValidReading)
            {
                return false;
            }
            bool hasSolid = false;
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger
                    || collider.attachedRigidbody != _body || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }
                hasSolid = true;
                Bounds bounds = collider.bounds;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        bounds.max.y, (corner & 2) == 0 ? bounds.min.z : bounds.max.z);
                    if (!TrySampleSurface(point, out float surface) || point.y + clearance > surface)
                    {
                        return false;
                    }
                }
            }
            return hasSolid;
        }

        public bool TrySampleSurface(Vector3 position, out float height)
        {
            height = 0f;
            if (WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
            {
                height = world.SampleWaterHeight(position);
            }
            else if (WorldServices.TryGet<WaterSamplerHost>(out WaterSamplerHost host))
            {
                height = WaveModel.HeightAt(host.Inputs.Waves, host.Inputs.Modifiers,
                    position.x, position.z, (float)host.CurrentTime);
            }
            else
            {
                return false;
            }
            return !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private void ApplySimulation()
        {
            bool simulate = isActiveAndEnabled && _monster != null && _monster.netIdentity != null && _monster.isServer
                && _monster.IsInitialized && !_monster.IsDead && !_body.isKinematic && !_scriptedMotion;
            _buoyancy.IsSimulating = simulate;
            _body.useGravity = simulate;
            if (_drift.enabled != simulate)
            {
                _drift.enabled = simulate;
            }
        }
    }
}
