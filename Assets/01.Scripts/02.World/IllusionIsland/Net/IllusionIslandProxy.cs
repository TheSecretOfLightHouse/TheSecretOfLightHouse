using System;
using Lighthouse.Network.Session.Net;
using Lighthouse.World.IllusionIsland.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Net
{
    [DisallowMultipleComponent]
    public class IllusionIslandProxy : NetworkBehaviour, IDockDisembarkGate
    {
        [SyncVar(hook = nameof(OnVanishingChanged))]
        private bool _isVanishing;

        private IllusionConfig _config;
        private int _illusionId;
        private float _vanishAt;
        private bool _hasInitialized;

        public static event Action<IllusionIslandProxy> MafiaMarkReceived;

        public event Action<IllusionIslandProxy, bool> VanishingChanged;
        public event Action<int> Removed;

        public int IllusionId => _illusionId;
        public bool IsVanishing => _isVanishing;
        public bool IsMafiaMarked { get; private set; }
        public float VanishSeconds => _config != null ? _config.VanishSeconds : 0f;

        public override void OnStopServer()
        {
            if (_hasInitialized)
            {
                _hasInitialized = false;
                Removed?.Invoke(_illusionId);
            }
        }

        private void Update()
        {
            if (!isServer || !_isVanishing || Time.time < _vanishAt)
            {
                return;
            }

            NetworkServer.Destroy(gameObject);
        }

        [Server]
        public void ServerInitialize(IllusionConfig config, int illusionId)
        {
            _config = config;
            _illusionId = illusionId;
            _hasInitialized = config != null;
        }

        [Server]
        public void ServerMarkForMafia(NetworkConnectionToClient mafia)
        {
            if (mafia != null)
            {
                TargetMarkIllusion(mafia);
            }
        }

        public bool Evaluate(PlayerController player, out float damage)
        {
            damage = 0f;
            if (!NetworkServer.active || player == null || _config == null)
            {
                return true;
            }

            if (IsMafiaPlayer(player) || _isVanishing)
            {
                return true;
            }

            damage = _config.DisembarkDamage;
            _vanishAt = Time.time + _config.VanishSeconds;
            _isVanishing = true;
            return true;
        }

        private static bool IsMafiaPlayer(PlayerController player)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            return manager != null
                && player.connectionToClient != null
                && manager.Connections.TryGetActorId(player.connectionToClient, out uint actorId)
                && manager.Roles.IsMafia(actorId);
        }

        private void OnVanishingChanged(bool oldValue, bool newValue)
        {
            VanishingChanged?.Invoke(this, newValue);
        }

        [TargetRpc]
        private void TargetMarkIllusion(NetworkConnectionToClient target)
        {
            IsMafiaMarked = true;
            MafiaMarkReceived?.Invoke(this);
        }
    }
}
