using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Lighthouse.Network.Session.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // Lobby scene: crew slots, the ready count and the ready toggle.
    [DisallowMultipleComponent]
    public sealed class SessionLobbyView : MonoBehaviour
    {
        [SerializeField]
        private SessionLobbySlot[] _slots = new SessionLobbySlot[LighthouseNetworkManager.MaxPlayers];
        [SerializeField]
        private Text _readyCount;
        [SerializeField]
        private Text _guide;
        [SerializeField]
        private Button _ready;
        [SerializeField]
        private Image _readyImage;
        [SerializeField]
        private Text _readyLabel;
        [Tooltip("Ready button art. Without sprites the button is tinted with the colours below instead.")]
        [SerializeField]
        private Sprite _readySprite;
        [SerializeField]
        private Sprite _cancelSprite;
        [SerializeField]
        private Color _readyColor = new Color32(64, 115, 97, 255);
        [SerializeField]
        private Color _cancelColor = new Color32(128, 52, 52, 255);
        [SerializeField]
        private Button _leave;
        [SerializeField]
        private Text _leaveLabel;
        [SerializeField]
        private GameObject _hostAddress;
        [SerializeField]
        private Text _hostAddressText;

        private readonly LobbyPlayer[] _sorted = new LobbyPlayer[LighthouseNetworkManager.MaxPlayers];

        // Last drawn values; texts are rebuilt only when one of these changes.
        private readonly uint[] _shownIds = new uint[LighthouseNetworkManager.MaxPlayers];
        private readonly byte[] _shownFlags = new byte[LighthouseNetworkManager.MaxPlayers];
        private int _shownPlayers = -1;
        private int _shownStart = -1;
        private int _shownHost = -1;

        private LobbyPlayer _localPlayer;

        public event Action LeaveRequested;

        // ActorId of this client's lobby player, kept after the lobby is gone so the end screen can tell "me" apart.
        public uint LocalActorId { get; private set; }

        private void Awake()
        {
            _ready.onClick.AddListener(HandleReady);
            _leave.onClick.AddListener(HandleLeave);
        }

        private void OnEnable()
        {
            _shownPlayers = -1;
            _shownHost = -1;
        }

        public void Refresh(int startPlayerCount, bool isHost)
        {
            int count = CollectSorted();
            int host = isHost ? 1 : 0;
            bool isDirty = count != _shownPlayers || startPlayerCount != _shownStart || host != _shownHost;
            int readyCount = 0;

            for (int i = 0; i < count; i++)
            {
                LobbyPlayer player = _sorted[i];

                if (player.IsReady)
                {
                    readyCount++;
                }

                if (_shownIds[i] != player.ActorId || _shownFlags[i] != FlagsOf(player))
                {
                    isDirty = true;
                }
            }

            if (!isDirty)
            {
                return;
            }

            if (host != _shownHost)
            {
                _hostAddress.SetActive(isHost);
                _leaveLabel.text = isHost ? "방 닫기" : "나가기";

                if (isHost)
                {
                    _hostAddressText.text = FindLanAddress();
                }
            }

            _shownPlayers = count;
            _shownStart = startPlayerCount;
            _shownHost = host;
            _localPlayer = null;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < count)
                {
                    LobbyPlayer player = _sorted[i];
                    _shownIds[i] = player.ActorId;
                    _shownFlags[i] = FlagsOf(player);

                    if (player.isLocalPlayer)
                    {
                        _localPlayer = player;
                        LocalActorId = player.ActorId;
                    }

                    _slots[i].ShowPlayer(player);
                }
                else
                {
                    if (i < _shownIds.Length)
                    {
                        _shownIds[i] = 0;
                        _shownFlags[i] = 0;
                    }

                    _slots[i].ShowEmpty();
                }
            }

            _readyCount.text = $"준비 {readyCount} / {count}";
            _guide.text = count < startPlayerCount
                ? $"선원을 기다리는 중입니다. ({count} / {startPlayerCount}명)\n모두 준비하면 자동으로 출항합니다."
                : "접속한 선원이 모두 준비하면\n자동으로 출항합니다.";

            bool isReady = _localPlayer && _localPlayer.IsReady;
            _ready.interactable = _localPlayer;
            _readyLabel.text = !_localPlayer ? "입장 중…" : isReady ? "준비 취소" : "준비하기";

            Sprite sprite = isReady ? _cancelSprite : _readySprite;

            if (sprite)
            {
                _readyImage.sprite = sprite;
                _readyImage.color = Color.white;
            }
            else
            {
                _readyImage.color = isReady ? _cancelColor : _readyColor;
            }
        }

        private static byte FlagsOf(LobbyPlayer player)
        {
            return (byte)((player.IsReady ? 1 : 0) | (player.isLocalPlayer ? 2 : 0));
        }

        private int CollectSorted()
        {
            IReadOnlyList<LobbyPlayer> players = LobbyPlayer.Active;
            int count = 0;

            for (int i = 0; i < players.Count && count < _sorted.Length; i++)
            {
                LobbyPlayer player = players[i];

                if (!player)
                {
                    continue;
                }

                // Insertion by ActorId keeps slot order stable as players come and go.
                int at = count;

                while (at > 0 && _sorted[at - 1].ActorId > player.ActorId)
                {
                    _sorted[at] = _sorted[at - 1];
                    at--;
                }

                _sorted[at] = player;
                count++;
            }

            for (int i = count; i < _sorted.Length; i++)
            {
                _sorted[i] = null;
            }

            return count;
        }

        private void HandleReady()
        {
            if (_localPlayer)
            {
                _localPlayer.RequestSetReady(!_localPlayer.IsReady);
            }
        }

        private void HandleLeave()
        {
            LeaveRequested?.Invoke();
        }

        // Adapters without a gateway are usually virtual (Hyper-V, VPN), so the first IPv4 with one is the LAN address.
        private static string FindLanAddress()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties = adapter.GetIPProperties();

                    if (properties.GatewayAddresses.Count == 0)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return address.Address.ToString();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(SessionLobbyView)}] Could not read the LAN address: {exception.Message}");
            }

            return "확인 불가";
        }
    }
}
