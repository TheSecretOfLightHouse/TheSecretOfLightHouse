using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.View;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Mafia.IdentityCheck.View
{
    // The mafia's identity check and summon (2026-10-23 sketch): two buttons above the vote button, a confirm box in the
    // vote screen's style before either request, and a notice with the server's answer. Only the mafia receives the
    // identity state, so the buttons never show for a citizen. The server decides; this screen only asks.
    [DisallowMultipleComponent]
    public sealed class IdentityActionScreen : MonoBehaviour
    {
        private const string Highlight = "#F6C64A";

        private enum Action : byte
        {
            None = 0,
            Check = 1,
            Summon = 2
        }

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;

        [Header("Buttons")]
        [SerializeField]
        private GameObject _hud;
        [SerializeField]
        private Button _checkButton;
        [SerializeField]
        private Button _summonButton;
        [SerializeField]
        private Text _status;
        [Tooltip("Tint of a button that cannot be pressed now.")]
        [SerializeField]
        private Color _unavailableColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);

        [Header("Confirm box")]
        [SerializeField]
        private GameObject _confirm;
        [SerializeField]
        private Text _confirmTitle;
        [SerializeField]
        private Text _confirmMessage;
        [SerializeField]
        private Text _confirmDetail;
        [SerializeField]
        private Text _confirmWarning;
        [SerializeField]
        private Button _acceptButton;
        [SerializeField]
        private Text _acceptLabel;
        [SerializeField]
        private Button _cancelButton;

        [Header("Notice")]
        [SerializeField]
        private GameObject _notice;
        [SerializeField]
        private Text _noticeTitle;
        [SerializeField]
        private Text _noticeMessage;
        [SerializeField]
        private Button _noticeButton;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private NetworkIdentity _player;
        private PlayerInput _playerInput;
        private Action _confirmAction;
        private Action _awaitedAction;
        private bool _isOverlayOpen;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        // Last shown values; text is rebuilt only when they change.
        private int _shownChecks = -1;
        private int _shownSummonSeconds = -1;
        private int _shownConfirmValue = -1;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _checkButton.onClick.AddListener(HandleCheckClicked);
            _summonButton.onClick.AddListener(HandleSummonClicked);
            _acceptButton.onClick.AddListener(HandleAcceptClicked);
            _cancelButton.onClick.AddListener(CloseOverlay);
            _noticeButton.onClick.AddListener(CloseOverlay);
            _hud.SetActive(false);
            _confirm.SetActive(false);
            _notice.SetActive(false);
        }

        private void OnEnable()
        {
            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>(FindObjectsInactive.Include);
            }

            if (_voting)
            {
                _voting.IdentityResponded += HandleIdentityResponded;
            }
        }

        private void Update()
        {
            if (_player != NetworkClient.localPlayer)
            {
                BindLocalPlayer(NetworkClient.localPlayer);
            }

            bool hasState = _voting && _voting.HasOwnIdentityState;

            if (!hasState)
            {
                CloseOverlay();
                _awaitedAction = Action.None;
                _hud.SetActive(false);
                return;
            }

            IdentityResponse state = _voting.OwnIdentityState;
            _hud.SetActive(!_isOverlayOpen);
            RefreshButtons(in state);

            if (_confirm.activeSelf)
            {
                RefreshConfirm(in state);
            }

            Keyboard keyboard = Keyboard.current;

            if (_isOverlayOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseOverlay();
            }
        }

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
            }

            CloseOverlay();
            BindLocalPlayer(null);
        }

        private void RefreshButtons(in IdentityResponse state)
        {
            SetAvailable(_checkButton, state.CanCheck);
            SetAvailable(_summonButton, state.CanSummon);
            int checks = state.RemainingCheckCount;
            int summonSeconds = state.HasSummonRight ? SecondsLeft(state.SummonExpiresAt) : -1;

            if (checks == _shownChecks && summonSeconds == _shownSummonSeconds)
            {
                return;
            }

            _shownChecks = checks;
            _shownSummonSeconds = summonSeconds;
            _status.text = summonSeconds >= 0
                ? $"소환 가능 <color={Highlight}>{summonSeconds}초</color>  ·  정체 확인 {checks}회 남음"
                : $"정체 확인 <color={Highlight}>{checks}회</color> 남음";
        }

        private void SetAvailable(Button button, bool isAvailable)
        {
            if (button.interactable == isAvailable)
            {
                return;
            }

            button.interactable = isAvailable;
            button.targetGraphic.color = isAvailable ? Color.white : _unavailableColor;
        }

        private void HandleCheckClicked()
        {
            OpenConfirm(Action.Check);
        }

        private void HandleSummonClicked()
        {
            OpenConfirm(Action.Summon);
        }

        private void OpenConfirm(Action action)
        {
            if (_isOverlayOpen || _awaitedAction != Action.None || SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }

            _confirmAction = action;
            bool isCheck = action == Action.Check;
            _confirmTitle.text = isCheck ? "정체 확인" : "크툴루 소환";
            _confirmMessage.text = isCheck ? "지금 정체를 확인할까요?" : "크툴루를 소환할까요?";
            _confirmWarning.text = isCheck
                ? "성공하면 10초 동안 소환할 수 있고, 실패하면 무고한 시민 한 명이 공개됩니다"
                : "소환하면 내 정체가 모두에게 공개되고 최후 전투가 시작됩니다";
            _acceptLabel.text = isCheck ? "확인하기" : "소환하기";
            _shownConfirmValue = -1;
            OpenOverlay(_confirm);
            RefreshConfirm(_voting.OwnIdentityState);
        }

        // The right can run out or the last check can go while the box is open; then the box closes with the reason.
        private void RefreshConfirm(in IdentityResponse state)
        {
            if (_confirmAction == Action.Check)
            {
                if (!state.CanCheck)
                {
                    ShowNotice("정체 확인", "지금은 정체를 확인할 수 없습니다.");
                    return;
                }

                if (state.RemainingCheckCount != _shownConfirmValue)
                {
                    _shownConfirmValue = state.RemainingCheckCount;
                    _confirmDetail.text = $"남은 확인 <color={Highlight}>{_shownConfirmValue}회</color>";
                }

                return;
            }

            if (!state.CanSummon)
            {
                ShowNotice("크툴루 소환", "소환 권리 시간이 지났습니다.");
                return;
            }

            int seconds = SecondsLeft(state.SummonExpiresAt);

            if (seconds != _shownConfirmValue)
            {
                _shownConfirmValue = seconds;
                _confirmDetail.text = $"소환 권리 <color={Highlight}>{seconds}초</color> 남음";
            }
        }

        private void HandleAcceptClicked()
        {
            Action action = _confirmAction;
            CloseOverlay();

            if (!_voting || !_voting.HasOwnIdentityState)
            {
                return;
            }

            _awaitedAction = action;

            if (action == Action.Check)
            {
                _voting.RequestIdentityCheck();
            }
            else if (action == Action.Summon)
            {
                _voting.RequestSummon();
            }
        }

        // State updates also arrive as responses; only the answer to this screen's own request opens the notice.
        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (_awaitedAction == Action.None || response.Result == IdentityActionResult.StateRetrieved)
            {
                return;
            }

            _awaitedAction = Action.None;
            ShowNotice(NoticeTitle(response.Result), NoticeMessage(response.Result));
        }

        private void ShowNotice(string title, string message)
        {
            if (_isOverlayOpen)
            {
                CloseOverlay();
            }

            if (SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }

            _noticeTitle.text = title;
            _noticeMessage.text = message;
            OpenOverlay(_notice);
        }

        private void OpenOverlay(GameObject box)
        {
            _isOverlayOpen = true;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            _hud.SetActive(false);
            box.SetActive(true);
            LockPlayer(true);
        }

        private void CloseOverlay()
        {
            if (!_isOverlayOpen)
            {
                return;
            }

            _isOverlayOpen = false;
            _confirmAction = Action.None;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _confirm.SetActive(false);
            _notice.SetActive(false);
            LockPlayer(false);
        }

        private static string NoticeTitle(IdentityActionResult result)
        {
            switch (result)
            {
                case IdentityActionResult.CheckSucceeded:
                    return "정체 확인 성공";
                case IdentityActionResult.CheckFailed:
                    return "정체 확인 실패";
                case IdentityActionResult.SummonApproved:
                    return "크툴루 소환";
                default:
                    return "요청 거절";
            }
        }

        private static string NoticeMessage(IdentityActionResult result)
        {
            switch (result)
            {
                case IdentityActionResult.CheckSucceeded:
                    return "10초 안에 크툴루를 소환할 수 있습니다.";
                case IdentityActionResult.CheckFailed:
                    return "무고한 시민 한 명의 정체가 모두에게 공개되었습니다.";
                case IdentityActionResult.SummonApproved:
                    return "크툴루가 깨어났습니다. 최후 전투가 시작됩니다.";
                case IdentityActionResult.NoChecksRemaining:
                    return "남은 정체 확인 기회가 없습니다.";
                case IdentityActionResult.SummonWindowActive:
                    return "소환 권리가 남아 있는 동안에는 다시 확인할 수 없습니다.";
                case IdentityActionResult.NoSummonRight:
                    return "먼저 정체 확인에 성공해야 소환할 수 있습니다.";
                case IdentityActionResult.SummonRightExpired:
                    return "소환 권리 시간이 지났습니다.";
                case IdentityActionResult.Downed:
                    return "난파 중에는 할 수 없습니다.";
                case IdentityActionResult.AlreadySummoned:
                    return "이미 크툴루를 소환했습니다.";
                case IdentityActionResult.GameEnded:
                case IdentityActionResult.RitualCompleted:
                    return "판이 끝나 할 수 없습니다.";
                default:
                    return "요청을 처리하지 못했습니다. 다시 시도해 주세요.";
            }
        }

        private static int SecondsLeft(double endsAt)
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)(endsAt - NetworkTime.time)));
        }

        private void BindLocalPlayer(NetworkIdentity player)
        {
            _player = player;
            _playerInput = player ? player.GetComponent<PlayerInput>() : null;
        }

        // Same stand-in input lock as the vote screen until the player side has one.
        private void LockPlayer(bool isLocked)
        {
            if (isLocked)
            {
                if (!_hasSavedCursor)
                {
                    _hasSavedCursor = true;
                    _savedLockMode = Cursor.lockState;
                    _savedCursorVisible = Cursor.visible;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (_playerInput)
                {
                    _wasPlayerInputEnabled = _playerInput.enabled;
                    _playerInput.enabled = false;
                }

                return;
            }

            if (_hasSavedCursor)
            {
                _hasSavedCursor = false;
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }

            if (_playerInput && _wasPlayerInputEnabled)
            {
                _playerInput.enabled = true;
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
    }
}
