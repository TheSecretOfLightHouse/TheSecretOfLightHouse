using Mirror;
using UnityEngine;

namespace Lighthouse.World.Ocean.Net
{
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuoyantObject), typeof(NetworkIdentity))]
    public sealed class BoatBuoyancyAuthority : NetworkBehaviour
    {
        private BuoyantObject _buoyancy;
        private Rigidbody _body;
        private bool _hasStoppedClient;

        private void Awake()
        {
            bool hasBuoyancy = TryGetComponent(out _buoyancy);
            bool hasBody = TryGetComponent(out _body);

            if (!hasBuoyancy || !hasBody)
            {
                if (_buoyancy != null)
                {
                    _buoyancy.IsSimulating = false;
                }

                Debug.LogError(
                    $"[{nameof(BoatBuoyancyAuthority)}] BuoyantObject and Rigidbody are required.",
                    this);

                enabled = false;
                return;
            }

            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void FixedUpdate()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Refresh();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Refresh();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            _hasStoppedClient = false;
            Refresh();
        }

        public override void OnStopClient()
        {
            _hasStoppedClient = true;
            Refresh();

            base.OnStopClient();
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            Refresh();
        }

        public override void OnStopAuthority()
        {
            if (_buoyancy != null)
            {
                _buoyancy.IsSimulating = false;
            }

            base.OnStopAuthority();
        }

        private void Refresh()
        {
            if (_buoyancy == null)
            {
                return;
            }

            bool isNetworkActive = NetworkServer.active || NetworkClient.active;
            bool hasAuthority = !isNetworkActive
                || (netIdentity != null && isClient && isOwned);

            _buoyancy.IsSimulating = isActiveAndEnabled
                && !_hasStoppedClient
                && _body != null
                && !_body.isKinematic
                && hasAuthority;
        }
    }
}
