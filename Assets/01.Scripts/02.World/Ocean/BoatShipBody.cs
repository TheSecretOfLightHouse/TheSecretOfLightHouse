using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatShipBody : MonoBehaviour, IShipBody
    {
        private const float MinMass = 0.01f;

        [SerializeField] private Rigidbody _body;
        [SerializeField] private uint _offlineShipId = 1;
        [SerializeField, Min(MinMass)] private float _mass = 500f;
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.3f, 0f);
        [SerializeField, Min(0f)] private float _rightingStrength = 1f;
        [SerializeField, Min(0f)] private float _sailArea = 4f;

        [SerializeField]
        private Vector3[] _buoyancyPoints =
        {
            new Vector3(-0.4f, -0.5f, -0.4f),
            new Vector3(0.4f, -0.5f, -0.4f),
            new Vector3(-0.4f, -0.5f, 0f),
            new Vector3(0.4f, -0.5f, 0f),
            new Vector3(-0.4f, -0.5f, 0.4f),
            new Vector3(0.4f, -0.5f, 0.4f)
        };

        private NetworkIdentity _identity;

        public uint ShipId => _identity != null && _identity.netId != 0 ? _identity.netId : _offlineShipId;

        public Rigidbody Body
        {
            get
            {
                if (_body == null)
                {
                    TryGetComponent(out _body);
                }

                return _body;
            }
        }

        public IReadOnlyList<Vector3> BuoyancyPoints => _buoyancyPoints;

        public float Mass => _mass;

        public Vector3 CenterOfMassOffset => _centerOfMassOffset;

        public float SailArea => _sailArea;

        public float RightingStrength => _rightingStrength;

        private void Awake()
        {
            Rigidbody body = Body;
            if (body == null || _buoyancyPoints == null || _buoyancyPoints.Length == 0)
            {
                Debug.LogError($"[{nameof(BoatShipBody)}] Rigidbody and buoyancy points are required.", this);
                enabled = false;
                return;
            }

            TryGetComponent(out _identity);
            body.mass = _mass;
            body.centerOfMass = _centerOfMassOffset;
            body.useGravity = true;

            if (!TryGetComponent(out WaveDriftBody _))
            {
                gameObject.AddComponent<WaveDriftBody>();
            }
        }
    }
}
