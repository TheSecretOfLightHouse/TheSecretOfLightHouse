using Mirror;
using UnityEngine;

namespace Lighthouse.Tower.Net
{
    // A place where an abyss crystal surfaces once the monster is summoned (2026-10-19 trial). The lighthouse
    // turns the spots on and counts what each player carries; the spot only knows whether a crystal is there.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class AbyssCrystalSpot : NetworkBehaviour
    {
        [Tooltip("Shown while a crystal can be picked up.")]
        [SerializeField]
        private GameObject _visual;

        [SyncVar(hook = nameof(OnIsAvailableChanged))]
        private bool _isAvailable;

        // Server-only. 0 = no respawn pending.
        private double _respawnAt;
        private bool _isActive;

        public bool IsAvailable => _isAvailable;

        private void Update()
        {
            if (!isServer || !_isActive || _isAvailable || _respawnAt <= 0 || NetworkTime.time < _respawnAt)
            {
                return;
            }

            _respawnAt = 0;
            _isAvailable = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RefreshVisual();
        }

        public override void OnStopServer()
        {
            _isActive = false;
            _respawnAt = 0;
            base.OnStopServer();
        }

        [Server]
        public void Activate()
        {
            _isActive = true;
            _respawnAt = 0;
            _isAvailable = true;
        }

        [Server]
        public void Deactivate()
        {
            _isActive = false;
            _respawnAt = 0;
            _isAvailable = false;
        }

        // Requests run one at a time on the server, so two players reaching together get one crystal between them.
        [Server]
        public bool TryTake(float respawnSeconds)
        {
            if (!_isActive || !_isAvailable)
            {
                return false;
            }

            _isAvailable = false;
            _respawnAt = NetworkTime.time + respawnSeconds;
            return true;
        }

        private void OnIsAvailableChanged(bool oldValue, bool newValue)
        {
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_visual)
            {
                _visual.SetActive(_isAvailable);
            }
        }
    }
}
