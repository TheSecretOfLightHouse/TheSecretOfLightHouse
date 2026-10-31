using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lighthouse.Network.Session.View
{
    // First screen, lobby, loading and end notices for the session flow (JH07).
    // Place the prefab in the offline scene. The first instance survives scene changes because Mirror replaces the
    // manager when it returns to the offline scene; the copy that scene brings along removes itself.
    [DisallowMultipleComponent]
    public sealed class SessionScreen : MonoBehaviour
    {
        private const string AddressKey = "Lighthouse.Session.LastAddress";
        private const string NicknameKey = "Lighthouse.Session.Nickname";
        private const string DefaultAddress = "localhost";

        private static SessionScreen _current;
        private static readonly HashSet<Object> GameplayOverlays = new HashSet<Object>();
        private static readonly HashSet<Object> LeaveControls = new HashSet<Object>();
        private static int _overlayClosedFrame = -1;

        [SerializeField]
        private GameObject _background;
        [SerializeField]
        private SessionTitleView _title;
        [SerializeField]
        private SessionLobbyView _lobby;
        [SerializeField]
        private SessionLoadingView _loading;
        [Tooltip("Fallback leave control for game scenes without a SessionLeaveButton (the in-game HUD's Esc hint).")]
        [SerializeField]
        private GameObject _gameMenu;
        [SerializeField]
        private Button _gameMenuLeave;
        [SerializeField]
        private SessionDialogView _dialog;
        [SerializeField]
        private EventSystem _eventSystem;
        [Tooltip("Replaces every Text font with an OS font at start. Turn off once the texts use a font asset with Korean glyphs.")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private Mode _mode;
        private bool _hasMode;

        // Join attempt tracking: a client that never reached the lobby gets no end record from the manager.
        private bool _isAttempting;
        private bool _hasConnected;
        private bool _hasReachedLobby;
        private bool _isCancelling;
        private bool _isEndAcknowledged;

        // The game may lock the cursor; the in-game leave dialog borrows it and gives it back.
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;

        private enum Mode
        {
            Hidden,
            Title,
            Lobby,
            Loading,
            InGame,
            Closing
        }

        // Lets development overlays step aside while the real screens are up.
        public static bool IsShown => _current && _current.isActiveAndEnabled;

        private void Awake()
        {
            if (_current && _current != this)
            {
                // Deactivate first so this copy's EventSystem leaves the active list before sceneLoaded runs
                // RefreshEventSystem on the surviving screen; otherwise that one would yield to a dying copy.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            _current = this;
            DontDestroyOnLoad(gameObject);

            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _title.JoinRequested += HandleJoin;
            _title.HostRequested += HandleHost;
            _title.CancelRequested += HandleCancelJoin;
            _title.QuitRequested += HandleQuit;
            _title.Address = PlayerPrefs.GetString(AddressKey, DefaultAddress);
            _title.Nickname = PlayerPrefs.GetString(NicknameKey, string.Empty);
            _lobby.LeaveRequested += HandleLeaveLobby;
            _gameMenuLeave.onClick.AddListener(ShowLeaveMatch);

            _dialog.Hide();
            ApplyMode(Mode.Hidden);
            RefreshEventSystem();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            Mode mode = ResolveMode(manager);

            if (!_hasMode || mode != _mode)
            {
                ApplyMode(mode);
            }

            if (!manager)
            {
                return;
            }

            TrackJoinAttempt(manager);

            switch (mode)
            {
                case Mode.Title:
                    _title.SetConnecting(NetworkClient.active && !NetworkClient.isConnected);
                    ShowSessionEnd(manager);
                    break;
                case Mode.Lobby:
                    _lobby.Refresh(manager.StartPlayerCount, NetworkServer.active);
                    break;
                case Mode.Loading:
                    _loading.ShowLoading(manager.ClientState.LoadedCount, manager.ClientState.PlayerCount);
                    break;
                case Mode.Closing:
                    _loading.ShowClosing();
                    break;
                case Mode.InGame:
                    if (!_dialog.IsOpen && _hasSavedCursor)
                    {
                        RestoreCursor();
                    }

                    // An open overlay owns Escape, so the hint steps aside with it.
                    bool showGameMenu = LeaveControls.Count == 0 && !IsGameplayOverlayOpen;

                    if (_gameMenu.activeSelf != showGameMenu)
                    {
                        _gameMenu.SetActive(showGameMenu);
                    }

                    break;
            }

            HandleEscape(mode);
        }

        private void OnDestroy()
        {
            if (_current != this)
            {
                return;
            }

            _current = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>Reserves Escape for a local gameplay overlay, including the frame in which it closes.</summary>
        public static void SetGameplayOverlayOpen(Object owner, bool isOpen)
        {
            if (isOpen && owner)
            {
                GameplayOverlays.Add(owner);
            }
            else if (GameplayOverlays.Remove(owner))
            {
                _overlayClosedFrame = Time.frameCount;
            }
        }

        /// <summary>True while a gameplay overlay is open or closed this frame, so another overlay does not open on the same key press.</summary>
        public static bool IsGameplayOverlayOpen => GameplayOverlays.Count > 0 || _overlayClosedFrame == Time.frameCount;

        /// <summary>Registers a leave control shown elsewhere, which hides this screen's fallback button.</summary>
        public static void SetLeaveControlShown(Object owner, bool isShown)
        {
            if (isShown && owner)
            {
                LeaveControls.Add(owner);
            }
            else
            {
                LeaveControls.Remove(owner);
            }
        }

        /// <summary>Opens the leave-match dialog during a match, as Escape does.</summary>
        public static void RequestLeaveMatch()
        {
            if (_current && _current._mode == Mode.InGame && !IsGameplayOverlayOpen)
            {
                _current.ShowLeaveMatch();
            }
        }

        private static Mode ResolveMode(LighthouseNetworkManager manager)
        {
            if (!manager)
            {
                return Mode.Hidden;
            }

            bool isServer = NetworkServer.active;

            if (!isServer && (!NetworkClient.active || !NetworkClient.isConnected))
            {
                return Mode.Title;
            }

            switch (manager.ClientState.Phase)
            {
                case MatchPhase.Loading:
                    return Mode.Loading;
                case MatchPhase.InProgress:
                    return Mode.InGame;
                case MatchPhase.Ended:
                    return Mode.Closing;
                default:
                    return Mode.Lobby;
            }
        }

        private void ApplyMode(Mode mode)
        {
            bool wasOffline = !_hasMode || _mode == Mode.Title || _mode == Mode.Hidden;
            _mode = mode;
            _hasMode = true;

            // An end notice raised on the first screen must survive the frame where the old manager is gone
            // and the offline scene's manager has not woken yet (Hidden); any other change closes the dialog.
            if (!wasOffline || (mode != Mode.Title && mode != Mode.Hidden))
            {
                _dialog.Hide();
            }

            _background.SetActive(mode == Mode.Title || mode == Mode.Lobby || mode == Mode.Loading || mode == Mode.Closing);
            _title.gameObject.SetActive(mode == Mode.Title);
            _lobby.gameObject.SetActive(mode == Mode.Lobby);
            _loading.gameObject.SetActive(mode == Mode.Loading || mode == Mode.Closing);
            _gameMenu.SetActive(mode == Mode.InGame && LeaveControls.Count == 0 && !IsGameplayOverlayOpen);

            if (mode == Mode.InGame)
            {
                return;
            }

            // Menus need a free cursor even if the game scene locked it before the match ended.
            _hasSavedCursor = false;

            if (mode != Mode.Hidden)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void ApplyOsFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);
            Text[] texts = GetComponentsInChildren<Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        private void TrackJoinAttempt(LighthouseNetworkManager manager)
        {
            if (NetworkClient.active && !NetworkServer.active)
            {
                _isAttempting = true;
                _hasConnected |= NetworkClient.isConnected;
                _hasReachedLobby |= manager.ClientState.PlayerCount > 0;
                return;
            }

            if (!_isAttempting || NetworkClient.active)
            {
                return;
            }

            if (!_hasReachedLobby && !_isCancelling && !manager.HasSessionEnd)
            {
                _dialog.Show(
                    "참가 실패",
                    _hasConnected
                        ? "호스트가 참가를 받지 않았습니다.\n방이 가득 찼을 수 있습니다."
                        : "호스트에 연결하지 못했습니다.\n주소와 호스트 상태를 확인하세요.",
                    "확인",
                    null);
            }

            _isAttempting = false;
            _hasConnected = false;
            _hasReachedLobby = false;
            _isCancelling = false;
        }

        private void ShowSessionEnd(LighthouseNetworkManager manager)
        {
            if (!manager.HasSessionEnd)
            {
                _isEndAcknowledged = false;
                return;
            }

            if (_isEndAcknowledged || _dialog.IsOpen)
            {
                return;
            }

            _isEndAcknowledged = true;

            if (SessionEndText.TryDescribe(manager.LastSessionEnd, _lobby.LocalActorId, out string title, out string message))
            {
                _dialog.Show(title, message, "확인", null);
            }
        }

        private void HandleEscape(Mode mode)
        {
            if (mode == Mode.InGame && (GameplayOverlays.Count > 0 || _overlayClosedFrame == Time.frameCount))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Game scene overlays can be destroyed without closing, so dead entries are dropped first; this runs only on
            // the key press.
            if (mode == Mode.Lobby)
            {
                GameplayOverlays.RemoveWhere(IsDestroyedOverlay);

                if (GameplayOverlays.Count > 0)
                {
                    return;
                }
            }

            if (_dialog.IsOpen)
            {
                _dialog.Hide();
            }
            else if (mode == Mode.Title)
            {
                HandleQuit();
            }
            else if (mode == Mode.Lobby)
            {
                HandleLeaveLobby();
            }
            else if (mode == Mode.InGame)
            {
                ShowLeaveMatch();
            }
        }

        private static bool IsDestroyedOverlay(Object owner)
        {
            return !owner;
        }

        private void HandleJoin()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (!manager)
            {
                return;
            }

            string address = _title.Address;

            if (string.IsNullOrEmpty(address))
            {
                address = DefaultAddress;
                _title.Address = address;
            }

            PlayerPrefs.SetString(AddressKey, address);
            SaveNickname();
            _isCancelling = false;
            manager.networkAddress = address;
            manager.StartClient();
        }

        private void HandleHost()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager)
            {
                SaveNickname();
                manager.StartHost();
            }
        }

        // Also saves the address set just before; PlayerPrefs.Save writes every pending key.
        private void SaveNickname()
        {
            string nickname = _title.Nickname;
            LobbyPlayer.LocalNickname = nickname;
            PlayerPrefs.SetString(NicknameKey, nickname);
            PlayerPrefs.Save();
        }

        private void HandleCancelJoin()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            _isCancelling = true;

            if (manager)
            {
                manager.StopClient();
            }
        }

        private void HandleQuit()
        {
            if (_title.IsEntryScreen && _title.IsEntryOpen)
            {
                if (NetworkClient.active)
                {
                    HandleCancelJoin();
                    return;
                }

                SaveNickname();
                _title.ShowMenu();
                return;
            }

            _dialog.Show("게임 종료", "게임을 종료할까요?", "종료", Application.Quit, "취소", null, true);
        }

        private void HandleLeaveLobby()
        {
            if (NetworkServer.active)
            {
                _dialog.Show("방 닫기", "방을 닫으면 모든 선원이\n첫 화면으로 돌아갑니다.", "방 닫기", LeaveSession, "취소", null, true);
                return;
            }

            LeaveSession();
        }

        private void ShowLeaveMatch()
        {
            if (_dialog.IsOpen)
            {
                return;
            }

            SaveCursor();
            _dialog.Show("판 나가기", "지금 나가면 모든 선원의\n판이 종료됩니다.", "나가기", LeaveSession, "계속하기", null, true);
        }

        private static void LeaveSession()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager)
            {
                manager.LeaveSession();
            }
        }

        private void SaveCursor()
        {
            if (!_hasSavedCursor)
            {
                _hasSavedCursor = true;
                _savedLockMode = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            _hasSavedCursor = false;
            Cursor.lockState = _savedLockMode;
            Cursor.visible = _savedCursorVisible;
        }

        // A scene may bring its own EventSystem; only one should drive input, and ours yields to it.
        private void RefreshEventSystem()
        {
            if (!_eventSystem)
            {
                return;
            }

            _eventSystem.gameObject.SetActive(false);

            if (!EventSystem.current)
            {
                _eventSystem.gameObject.SetActive(true);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshEventSystem();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            LeaveControls.Clear();
        }
    }
}
