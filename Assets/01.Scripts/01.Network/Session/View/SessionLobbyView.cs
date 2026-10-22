using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // Lobby scene: crew slots, the ready count and the ready toggle.
    // Progress pips, address copy, join/leave notices and ping are optional; unassigned references are skipped.
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
        [Tooltip("One pip per seat needed to start: ready pips first, then joined, then empty seats. Without sprites the pips are tinted instead.")]
        [SerializeField]
        private Image[] _readyPips = new Image[LighthouseNetworkManager.MaxPlayers];
        [SerializeField]
        private Sprite _pipReadySprite;
        [SerializeField]
        private Sprite _pipJoinedSprite;
        [SerializeField]
        private Color _pipReadyColor = new Color32(62, 112, 58, 255);
        [SerializeField]
        private Color _pipJoinedColor = new Color32(122, 94, 72, 255);
        [SerializeField]
        private float _pipEmptyAlpha = 0.25f;
        [Tooltip("Optional open pip for empty seats; drawn at full alpha instead of fading the joined pip.")]
        [SerializeField]
        private Sprite _pipEmptySprite;
        [Tooltip("Optional \"joined / seats\" count.")]
        [SerializeField]
        private Text _crewCount;
        [Tooltip("Optional one-line crew summary next to the list title.")]
        [SerializeField]
        private Text _crewSummary;
        [Tooltip("Optional crew icons, one per boat seat.")]
        [SerializeField]
        private Image[] _crewIcons = new Image[LighthouseNetworkManager.MaxPlayers];
        [SerializeField]
        private Sprite _crewIconJoined;
        [SerializeField]
        private Sprite _crewIconEmpty;
        [SerializeField]
        private Button _copyAddress;
        [SerializeField]
        private CanvasGroup _notice;
        [SerializeField]
        private Text _noticeText;
        [SerializeField]
        private float _noticeSeconds = 2.5f;
        [SerializeField]
        private float _noticeFadeSeconds = 0.4f;
        [Tooltip("Round-trip time to the host. Hidden on the host itself.")]
        [SerializeField]
        private Text _ping;
        [SerializeField]
        private float _pingInterval = 0.5f;
        [SerializeField]
        private int _pingGoodMs = 80;
        [SerializeField]
        private int _pingFairMs = 150;
        [SerializeField]
        private Color _pingGoodColor = new Color32(146, 206, 112, 255);
        [SerializeField]
        private Color _pingFairColor = new Color32(226, 186, 116, 255);
        [SerializeField]
        private Color _pingPoorColor = new Color32(214, 108, 96, 255);

        private readonly LobbyPlayer[] _sorted = new LobbyPlayer[LighthouseNetworkManager.MaxPlayers];

        // Last drawn values; texts are rebuilt only when one of these changes.
        private readonly uint[] _shownIds = new uint[LighthouseNetworkManager.MaxPlayers];
        private readonly byte[] _shownFlags = new byte[LighthouseNetworkManager.MaxPlayers];
        private readonly string[] _shownNames = new string[LighthouseNetworkManager.MaxPlayers];
        private int _shownPlayers = -1;
        private int _shownStart = -1;
        private int _shownHost = -1;
        private int _shownPingMs = -1;

        // Crew seen at the last refresh, for join/leave notices. Taken once our own player exists so the
        // players already aboard when we enter are not announced as joining.
        private readonly uint[] _knownIds = new uint[LighthouseNetworkManager.MaxPlayers];
        private readonly string[] _knownNames = new string[LighthouseNetworkManager.MaxPlayers];
        private int _knownCount;
        private bool _hasKnownCrew;

        private LobbyPlayer _localPlayer;
        private string _lanAddress;
        private float _noticeEndTime;
        private float _nextPingTime;

        public event Action LeaveRequested;

        // ActorId of this client's lobby player, kept after the lobby is gone so the end screen can tell "me" apart.
        public uint LocalActorId { get; private set; }

        private void Awake()
        {
            _ready.onClick.AddListener(HandleReady);
            _leave.onClick.AddListener(HandleLeave);

            if (_copyAddress)
            {
                _copyAddress.onClick.AddListener(HandleCopyAddress);
            }
        }

        private void OnEnable()
        {
            _shownPlayers = -1;
            _shownHost = -1;
            _shownPingMs = -1;
            _hasKnownCrew = false;
            _knownCount = 0;
            _noticeEndTime = 0f;

            if (_notice)
            {
                _notice.alpha = 0f;
            }
        }

        private void Update()
        {
            UpdateNotice();
            UpdatePing();
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

                if (_shownIds[i] != player.ActorId || _shownFlags[i] != FlagsOf(player) || _shownNames[i] != player.DisplayName)
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
                    _lanAddress = FindLanAddress();
                    _hostAddressText.text = _lanAddress ?? "확인 불가";
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
                    _shownNames[i] = player.DisplayName;

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
                        _shownNames[i] = null;
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

            ShowReadyPips(startPlayerCount, count, readyCount);
            ShowCrew(count);
            AnnounceCrewChanges(count);
        }

        // Counts against the boat's seats, not the start count: a solo test scene starts with 1 but still seats 4.
        private void ShowCrew(int count)
        {
            if (_crewCount)
            {
                _crewCount.text = $"{count} / {LighthouseNetworkManager.MaxPlayers}";
            }

            if (_crewSummary)
            {
                _crewSummary.text = $"선원 {count}명 승선";
            }

            for (int i = 0; i < _crewIcons.Length; i++)
            {
                Image icon = _crewIcons[i];

                if (!icon)
                {
                    continue;
                }

                bool isUsed = i < LighthouseNetworkManager.MaxPlayers;
                icon.gameObject.SetActive(isUsed);
                Sprite sprite = i < count ? _crewIconJoined : _crewIconEmpty;

                if (isUsed && sprite)
                {
                    icon.sprite = sprite;
                }
            }
        }

        private static byte FlagsOf(LobbyPlayer player)
        {
            return (byte)((player.IsReady ? 1 : 0) | (player.isLocalPlayer ? 2 : 0) | (player.IsHost ? 4 : 0));
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

        private void ShowReadyPips(int startPlayerCount, int count, int readyCount)
        {
            for (int i = 0; i < _readyPips.Length; i++)
            {
                Image pip = _readyPips[i];

                if (!pip)
                {
                    continue;
                }

                bool isUsed = i < startPlayerCount;
                pip.gameObject.SetActive(isUsed);

                if (!isUsed)
                {
                    continue;
                }

                if (i < readyCount)
                {
                    SetPip(pip, _pipReadySprite, _pipReadyColor, 1f);
                }
                else if (i < count)
                {
                    SetPip(pip, _pipJoinedSprite, _pipJoinedColor, 1f);
                }
                else if (_pipEmptySprite)
                {
                    SetPip(pip, _pipEmptySprite, _pipJoinedColor, 1f);
                }
                else
                {
                    SetPip(pip, _pipJoinedSprite, _pipJoinedColor, _pipEmptyAlpha);
                }
            }
        }

        private static void SetPip(Image pip, Sprite sprite, Color fallbackColor, float alpha)
        {
            Color color = Color.white;

            if (sprite)
            {
                pip.sprite = sprite;
            }
            else
            {
                color = fallbackColor;
            }

            color.a *= alpha;
            pip.color = color;
        }

        private void AnnounceCrewChanges(int count)
        {
            if (!_hasKnownCrew)
            {
                if (!_localPlayer)
                {
                    return;
                }

                RememberCrew(count);
                _hasKnownCrew = true;
                return;
            }

            int joined = 0;
            int left = 0;
            string joinedName = null;
            string leftName = null;

            for (int i = 0; i < count; i++)
            {
                LobbyPlayer player = _sorted[i];

                // A new player is announced once its nickname arrives, so the notice and its card agree.
                if (player.HasNickname && Array.IndexOf(_knownIds, player.ActorId, 0, _knownCount) < 0)
                {
                    joined++;
                    joinedName = player.DisplayName;
                }
            }

            for (int i = 0; i < _knownCount; i++)
            {
                if (!IsAboard(_knownIds[i], count))
                {
                    left++;
                    leftName = _knownNames[i];
                }
            }

            RememberCrew(count);

            // Leaving matters more than joining when both land in one refresh.
            if (left > 0)
            {
                ShowNotice(left == 1 ? $"{leftName}님이 배에서 내렸습니다." : $"선원 {left}명이 배에서 내렸습니다.");
            }
            else if (joined > 0)
            {
                ShowNotice(joined == 1 ? $"{joinedName}님이 승선했습니다." : $"선원 {joined}명이 승선했습니다.");
            }
        }

        private bool IsAboard(uint actorId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_sorted[i].ActorId == actorId)
                {
                    return true;
                }
            }

            return false;
        }

        // Unnamed players are left out so they are announced once named; the departed keep their last name.
        private void RememberCrew(int count)
        {
            _knownCount = 0;

            for (int i = 0; i < count; i++)
            {
                LobbyPlayer player = _sorted[i];

                if (player.HasNickname)
                {
                    _knownIds[_knownCount] = player.ActorId;
                    _knownNames[_knownCount] = player.DisplayName;
                    _knownCount++;
                }
            }
        }

        private void ShowNotice(string message)
        {
            if (!_notice || !_noticeText)
            {
                return;
            }

            _noticeText.text = message;
            _notice.alpha = 1f;
            _noticeEndTime = Time.unscaledTime + _noticeSeconds;
        }

        private void UpdateNotice()
        {
            if (!_notice || _notice.alpha <= 0f)
            {
                return;
            }

            float remaining = _noticeEndTime - Time.unscaledTime;
            _notice.alpha = _noticeFadeSeconds > 0f ? Mathf.Clamp01(remaining / _noticeFadeSeconds) : remaining > 0f ? 1f : 0f;
        }

        private void UpdatePing()
        {
            if (!_ping)
            {
                return;
            }

            // The host talks to itself; its round trip is always zero and says nothing about the others.
            bool isShown = NetworkClient.isConnected && !NetworkServer.active;

            if (_ping.enabled != isShown)
            {
                _ping.enabled = isShown;
            }

            if (!isShown || Time.unscaledTime < _nextPingTime)
            {
                return;
            }

            _nextPingTime = Time.unscaledTime + _pingInterval;
            int pingMs = Mathf.RoundToInt((float)(NetworkTime.rtt * 1000.0));

            if (pingMs == _shownPingMs)
            {
                return;
            }

            _shownPingMs = pingMs;
            _ping.text = $"연결 {pingMs}ms";
            _ping.color = pingMs <= _pingGoodMs ? _pingGoodColor : pingMs <= _pingFairMs ? _pingFairColor : _pingPoorColor;
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

        private void HandleCopyAddress()
        {
            if (string.IsNullOrEmpty(_lanAddress))
            {
                ShowNotice("복사할 주소를 찾지 못했습니다.");
                return;
            }

            GUIUtility.systemCopyBuffer = _lanAddress;
            ShowNotice("주소를 복사했습니다. 참가자에게 보내 주세요.");
        }

        // Adapters without a gateway are usually virtual (Hyper-V, VPN), so the first IPv4 with one is the LAN address.
        // Returns null when none is found.
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

            return null;
        }
    }
}
