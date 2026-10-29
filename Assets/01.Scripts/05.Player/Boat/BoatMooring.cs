using UnityEngine;

namespace Lighthouse.Player.Boat
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoatController))]
    public sealed class BoatMooring : MonoBehaviour
    {
        private Rigidbody _body;
        private BoatController _boat;
        private RigidbodyConstraints _freeConstraints;
        private Vector3 _anchor;
        private bool _isMoored;

        public bool IsMoored => _isMoored;
        public Vector3 Anchor => _anchor;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _boat = GetComponent<BoatController>();
        }

        private void FixedUpdate()
        {
            // Only the owner's physics instance chooses the berth. Remote copies follow its transform.
            bool isMooringRequired = _boat.CanSimulateLocalPhysics && !_boat.IsOccupied && !_boat.IsWrecked;
            if (!isMooringRequired)
            {
                Release();
                return;
            }

            if (!_isMoored)
            {
                _freeConstraints = _body.constraints;
                _anchor = _body.position;
                _body.constraints = _freeConstraints
                    | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                _isMoored = true;
            }

            // Preserve vertical buoyancy and all rotational constraints authored on the prefab.
            Vector3 velocity = _body.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            _body.linearVelocity = velocity;
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (_isMoored && _body)
            {
                _body.constraints = _freeConstraints;
            }

            _isMoored = false;
        }
    }

}
