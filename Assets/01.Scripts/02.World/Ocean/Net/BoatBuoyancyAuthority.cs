using Mirror;
using UnityEngine;

namespace Lighthouse.World.Ocean.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuoyancyBody), typeof(NetworkIdentity))]
    public sealed class BoatBuoyancyAuthority : NetworkBehaviour
    {
        private BuoyancyBody _buoyancy;

        private void Awake()
        {
            if (!TryGetComponent(out _buoyancy))
            {
                Debug.LogError($"[{nameof(BoatBuoyancyAuthority)}] BuoyancyBody is required.", this);
                enabled = false;
                return;
            }

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
            Refresh();
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            Refresh();
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            Refresh();
        }

        private void Refresh()
        {
            if (_buoyancy == null)
            {
                return;
            }

            bool isNetworkActive = NetworkServer.active || NetworkClient.active;
            _buoyancy.IsSimulating = !isNetworkActive || isOwned;
        }
    }
}
