using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class BoxShipBody : MonoBehaviour, IShipBody
    {
        [SerializeField] private uint _shipId = 1;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private float _mass = 500f;
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.3f, 0f);
        [SerializeField] private float _rightingStrength = 1f;

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

        public uint ShipId => _shipId;

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

        public float SailArea => 0f;

        public float RightingStrength => _rightingStrength;

        private void Awake()
        {
            Rigidbody body = Body;
            if (body == null)
            {
                Debug.LogError($"[{nameof(BoxShipBody)}] Rigidbody is required.");
                enabled = false;
                return;
            }

            body.mass = _mass;
            body.centerOfMass = _centerOfMassOffset;
        }
    }
}
