#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // Test aid for unattended build clients: with "-autoJoin" it joins localhost once and readies in the lobby.
    // Joins only once per process so returning to the first screen after a match does not start another.
    [DisallowMultipleComponent]
    public sealed class AutoJoinProbe : MonoBehaviour
    {
        private const string AutoJoinFlag = "-autoJoin";

        private static bool _hasJoined;

        [SerializeField]
        private LighthouseNetworkManager _manager;

        private bool _isActive;
        private bool _hasRequestedReady;

        private void Awake()
        {
            if (!_manager)
            {
                _manager = GetComponent<LighthouseNetworkManager>();
            }
        }

        private void Start()
        {
            if (_hasJoined || !_manager || Array.IndexOf(Environment.GetCommandLineArgs(), AutoJoinFlag) < 0)
            {
                return;
            }

            _hasJoined = true;
            _isActive = true;
            _manager.networkAddress = "localhost";
            _manager.StartClient();
            Debug.Log($"[{nameof(AutoJoinProbe)}] Joining localhost.");
        }

        private void Update()
        {
            if (!_isActive || _hasRequestedReady || !NetworkClient.isConnected || _manager.ClientState.Phase != MatchPhase.Lobby)
            {
                return;
            }

            if (NetworkClient.localPlayer && NetworkClient.localPlayer.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                lobbyPlayer.RequestSetReady(true);
                _hasRequestedReady = true;
                Debug.Log($"[{nameof(AutoJoinProbe)}] Ready requested.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _hasJoined = false;
        }
    }
}
#endif
