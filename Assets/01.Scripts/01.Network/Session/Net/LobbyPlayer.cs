using System.Collections.Generic;
using System.Text;
using Lighthouse.Network.Connections.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Session.Net
{
    // Lobby-scene player object. It is destroyed when the game scene loads and replaced by the game player,
    // so per-player lobby data (ready now, later possibly a chosen job) lives here and never in the game player.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkActor))]
    public sealed class LobbyPlayer : NetworkBehaviour
    {
        public const int MaxNicknameLength = 12;

        private static readonly List<LobbyPlayer> ActivePlayers = new List<LobbyPlayer>(LighthouseNetworkManager.MaxPlayers);

        [SyncVar]
        private bool _isReady;
        [SyncVar]
        private bool _isHost;
        [SyncVar]
        private string _nickname = string.Empty;

        private NetworkActor _actor;

        // Client-side list for lobby UI. The server counts readiness from its connections instead.
        public static IReadOnlyList<LobbyPlayer> Active => ActivePlayers;

        // Typed on the first screen before connecting; sent once our lobby player spawns.
        public static string LocalNickname { get; set; } = string.Empty;

        public bool IsReady => _isReady;
        public bool IsHost => _isHost;
        public uint ActorId => _actor.ActorId;
        public bool HasNickname => !string.IsNullOrEmpty(_nickname);
        public string DisplayName => HasNickname ? _nickname : $"플레이어 {ActorId}";

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        public override void OnStartServer()
        {
            // Remote clients cannot tell which connection is the listen host, so the server marks it once at spawn.
            _isHost = connectionToClient == NetworkServer.localConnection;
        }

        public override void OnStartClient()
        {
            ActivePlayers.Add(this);
        }

        // Sent even when empty so every player ends up named; the lobby waits for the name before announcing a join.
        public override void OnStartLocalPlayer()
        {
            CmdSetNickname(LocalNickname ?? string.Empty);
        }

        public override void OnStopClient()
        {
            ActivePlayers.Remove(this);
        }

        [Command]
        private void CmdSetNickname(string nickname)
        {
            string sanitized = SanitizeNickname(nickname);
            _nickname = sanitized.Length > 0 ? sanitized : $"플레이어 {ActorId}";
        }

        // Clients are not trusted: drop line breaks and control characters, trim, and cap the length.
        private static string SanitizeNickname(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(MaxNicknameLength);

            for (int i = 0; i < nickname.Length && builder.Length < MaxNicknameLength * 2; i++)
            {
                char c = nickname[i];

                if (!char.IsControl(c))
                {
                    builder.Append(c);
                }
            }

            string trimmed = builder.ToString().Trim();
            return trimmed.Length > MaxNicknameLength ? trimmed.Substring(0, MaxNicknameLength).TrimEnd() : trimmed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LocalNickname = string.Empty;
        }

        [Client]
        public void RequestSetReady(bool isReady)
        {
            if (isLocalPlayer)
            {
                CmdSetReady(isReady);
            }
        }

        [Command]
        private void CmdSetReady(bool isReady)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            // Readiness is frozen once loading starts; late toggles must not reopen the lobby.
            if (!manager || !manager.CanChangeReady)
            {
                return;
            }

            _isReady = isReady;
            manager.HandleReadyChanged();
        }
    }
}
