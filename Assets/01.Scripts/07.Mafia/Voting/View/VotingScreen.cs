using System.Collections.Generic;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.View;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.View
{
    // Secret vote UI (2026-10-22 sketch): an in-game button that shows the own votes left, the vote screen with one card
    // per other player, and the confirm asked before a vote is spent. The confirm stays on the picked card (2026-10-28
    // user decision): the ballot about to go blinks and a red line says it never comes back. The server decides every
    // vote; this screen only asks. The mafia cannot vote but sees the same screen with a pretend count that is never
    // sent, so the screen does not give the role away (win-vote-decisions 1).
    [DisallowMultipleComponent]
    public sealed class VotingScreen : MonoBehaviour
    {
        private const float RosterRefreshSeconds = 1f;
        private const float BallotBlinkSeconds = 0.25f;
        private const string Highlight = "#F6C64A";
        private const string Warning = "#EB7361";
        private const string Muted = "#A89E8C";

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private Key _openKey = Key.V;
        [SerializeField]
        private float _fadeInSeconds = 0.12f;

        [Header("In-game button")]
        [SerializeField]
        private GameObject _hud;
        [SerializeField]
        private Button _hudButton;
        [SerializeField]
        private Text _hudRemaining;
        [SerializeField]
        private Text _hudTarget;

        [Header("Vote screen")]
        [SerializeField]
        private GameObject _window;
        [Tooltip("One per vote, left first; used votes come first.")]
        [SerializeField]
        private Image[] _ballots = new Image[0];
        [SerializeField]
        private Sprite _ballotUsed;
        [SerializeField]
        private Sprite _ballotLeft;
        [SerializeField]
        private Text _remaining;
        [SerializeField]
        private VoteCandidateCard[] _cards = new VoteCandidateCard[0];
        [SerializeField]
        private Text _notice;
        [SerializeField]
        private Color _noticeColor = new Color(0.98f, 0.92f, 0.79f);
        [SerializeField]
        private Color _rejectColor = new Color(0.92f, 0.45f, 0.38f);

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<uint> _candidates = new List<uint>();
        private CanvasGroup _windowGroup;
        private float _openedAt;
        private NetworkIdentity _player;
        private PlayerInput _playerInput;
        private uint _ownActorId;
        private float _nextRosterAt;
        private bool _isOpen;
        private bool _isPending;
        private uint _pendingTarget;
        private float _pendingAt;
        // Built once per pick so the card text is not rebuilt every frame.
        private string _pendingPrompt;
        private uint _requestedTarget;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        // Seen when a mafia without identity state tries to vote; the pretend count takes over from then on.
        private bool _wasRefusedAsMafia;
        private uint _fakeTarget;
        private int _fakeRemaining = VoteService.MaxVoteCount;

        public bool IsOpen => _isOpen;

        private bool IsOwnMafia => _voting && (_voting.HasOwnIdentityState || _wasRefusedAsMafia);

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].Bind(i + 1, HandleCardClicked);
            }

            // Added here so a prefab built before the fade-in keeps working without a rebuild.
            if (!_window.TryGetComponent(out _windowGroup))
            {
                _windowGroup = _window.AddComponent<CanvasGroup>();
            }

            _hudButton.onClick.AddListener(Open);
            _window.SetActive(false);
            _hud.SetActive(false);
        }

        private void OnEnable()
        {
            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>(FindObjectsInactive.Include);
            }

            if (_voting)
            {
                _voting.VoteResponded += HandleVoteResponded;
            }
        }

        private void Update()
        {
            if (_player != NetworkClient.localPlayer)
            {
                BindLocalPlayer(NetworkClient.localPlayer);
            }

            bool hasState = _voting && _voting.HasOwnVoteState;

            if (!hasState)
            {
                ResetState();
            }

            _hud.SetActive(hasState && !_isOpen && !_voting.OwnVoteState.IsEnded);

            if (!hasState)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRosterAt)
            {
                RefreshRoster();
            }

            if (_voting.OwnVoteState.IsEnded && _isOpen)
            {
                Close();
            }

            Refresh();
            FadeIn();
            HandleKeys();
        }

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.VoteResponded -= HandleVoteResponded;
            }

            Close();
            BindLocalPlayer(null);
        }

        public void Open()
        {
            if (_isOpen || !_voting || !_voting.HasOwnVoteState || _voting.OwnVoteState.IsEnded
                || SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }

            _isOpen = true;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            _openedAt = Time.unscaledTime;
            _windowGroup.alpha = 0f;
            _window.SetActive(true);
            _pendingTarget = 0;
            SetNotice(string.Empty, _noticeColor);
            LockPlayer(true);
            RefreshRoster();
            Refresh();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _pendingTarget = 0;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _window.SetActive(false);
            LockPlayer(false);
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (!_isOpen)
            {
                if (keyboard[_openKey].wasPressedThisFrame)
                {
                    Open();
                }

                return;
            }

            if (_pendingTarget != 0)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    HandleConfirmClicked();
                }
                else if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseConfirm();
                }
            }
            else if (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame)
            {
                Close();
                return;
            }

            // While a pick waits, the same number confirms it and another number moves it, like a click.
            for (int i = 0; i < _cards.Length && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame && _cards[i].gameObject.activeSelf)
                {
                    _cards[i].Press();
                }
            }
        }

        private void Refresh()
        {
            VoteResponse vote = _voting.OwnVoteState;
            bool isMafia = IsOwnMafia;
            uint current = isMafia ? _fakeTarget : vote.TargetActorId;
            int remaining = isMafia ? _fakeRemaining : vote.RemainingVoteCount;
            // The mafia gets the same lock as everyone, so the screen locks at the same moment.
            bool isLocked = vote.IsLocked || vote.IsEnded;

            if (isLocked && _pendingTarget != 0)
            {
                CloseConfirm();
                SetNotice("점화 의식이 시작되어 투표가 잠겼습니다.", _rejectColor);
            }

            _hudRemaining.text = $"남은 지목  {remaining} / {VoteService.MaxVoteCount}";
            _hudTarget.text = isLocked ? "투표 잠김"
                : current != 0 ? $"내 지목: {GetDisplayName(current)}" : "내 지목 없음";

            if (!_isOpen)
            {
                return;
            }

            int used = VoteService.MaxVoteCount - remaining;
            // The ballot the pick would spend flips between used and left, starting on used.
            bool isBlinkUsed = ((int)((Time.unscaledTime - _pendingAt) / BallotBlinkSeconds) & 1) == 0;

            for (int i = 0; i < _ballots.Length; i++)
            {
                bool isUsed = i < used || (i == used && _pendingTarget != 0 && isBlinkUsed);
                _ballots[i].sprite = isUsed ? _ballotUsed : _ballotLeft;
            }

            _remaining.text = $"<color={Highlight}>{remaining}</color> / {VoteService.MaxVoteCount}";
            bool canPick = !isLocked && !_isPending;

            for (int i = 0; i < _cards.Length; i++)
            {
                bool hasCandidate = i < _candidates.Count;
                _cards[i].gameObject.SetActive(hasCandidate);

                if (hasCandidate)
                {
                    uint actorId = _candidates[i];
                    bool isPicked = actorId == _pendingTarget;
                    _cards[i].Show(actorId, GetDisplayName(actorId), actorId == current, canPick,
                        _pendingTarget != 0 && !isPicked, isPicked ? _pendingPrompt : null);
                }
            }
        }

        private void FadeIn()
        {
            if (_isOpen && _windowGroup.alpha < 1f)
            {
                _windowGroup.alpha = _fadeInSeconds > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - _openedAt) / _fadeInSeconds)
                    : 1f;
            }
        }

        private void HandleCardClicked(VoteCandidateCard card)
        {
            VoteResponse vote = _voting.OwnVoteState;
            bool isMafia = IsOwnMafia;
            uint current = isMafia ? _fakeTarget : vote.TargetActorId;
            int remaining = isMafia ? _fakeRemaining : vote.RemainingVoteCount;

            // A second click on the waiting card confirms it.
            if (card.ActorId == _pendingTarget)
            {
                HandleConfirmClicked();
            }
            // Re-picking the current target is free and needs no confirm; an empty count only says so.
            else if (card.ActorId == current)
            {
                SetNotice("이미 지목하고 있는 선원입니다.", _noticeColor);
            }
            else if (remaining <= 0)
            {
                SetNotice("남은 지목 기회가 없습니다.", _rejectColor);
            }
            else
            {
                SetNotice(string.Empty, _noticeColor);
                _pendingTarget = card.ActorId;
                _pendingAt = Time.unscaledTime;
                _pendingPrompt = $"남은 기회 {remaining}회 → <color={Highlight}>{remaining - 1}회</color>\n"
                    + $"<size=17><color={Warning}>기회 회복 및 투표 철회 불가</color></size>\n"
                    + $"<size=15><color={Muted}>Enter·다시 클릭 확정  /  ESC 취소</color></size>";
            }
        }

        private void HandleConfirmClicked()
        {
            uint target = _pendingTarget;

            if (target == 0)
            {
                return;
            }

            CloseConfirm();

            if (IsOwnMafia)
            {
                _fakeTarget = target;
                _fakeRemaining--;
                SetNotice($"{GetDisplayName(target)}님을 지목했습니다.", _noticeColor);
            }
            else
            {
                _isPending = true;
                _requestedTarget = target;
                _voting.RequestVote(target);
            }
        }

        private void CloseConfirm()
        {
            _pendingTarget = 0;
            _pendingPrompt = null;
        }

        // The server has the last word: a refused vote drops the waiting pick and says why.
        private void HandleVoteResponded(VoteResponse response)
        {
            if (response.Result == VoteChangeResult.StateRetrieved)
            {
                return;
            }

            _isPending = false;

            switch (response.Result)
            {
                case VoteChangeResult.Changed:
                    SetNotice($"{GetDisplayName(response.TargetActorId)}님을 지목했습니다.", _noticeColor);
                    break;
                case VoteChangeResult.Unchanged:
                    SetNotice("이미 지목하고 있는 선원입니다.", _noticeColor);
                    break;
                case VoteChangeResult.MafiaCannotVote:
                    // Only a match without identity state gets here; act as if the vote went through.
                    _wasRefusedAsMafia = true;
                    _fakeTarget = _requestedTarget;
                    _fakeRemaining--;
                    SetNotice($"{GetDisplayName(_requestedTarget)}님을 지목했습니다.", _noticeColor);
                    break;
                case VoteChangeResult.VotingLocked:
                case VoteChangeResult.GameEnded:
                    CloseConfirm();
                    SetNotice("점화 의식이 시작되어 투표가 잠겼습니다.", _rejectColor);
                    break;
                case VoteChangeResult.NoVotesRemaining:
                    CloseConfirm();
                    SetNotice("남은 지목 기회가 없습니다.", _rejectColor);
                    break;
                default:
                    CloseConfirm();
                    SetNotice("지목하지 못했습니다. 잠시 후 다시 시도하세요.", _rejectColor);
                    break;
            }
        }

        private void RefreshRoster()
        {
            _nextRosterAt = Time.unscaledTime + RosterRefreshSeconds;
            _candidates.Clear();

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity && identity.TryGetComponent(out NetworkActor actor)
                    && actor.ActorId != 0 && actor.ActorId != _ownActorId && !_candidates.Contains(actor.ActorId))
                {
                    _candidates.Add(actor.ActorId);
                }
            }

            _candidates.Sort();
        }

        private void BindLocalPlayer(NetworkIdentity player)
        {
            _player = player;
            _playerInput = player ? player.GetComponent<PlayerInput>() : null;
            NetworkActor actor = player ? player.GetComponent<NetworkActor>() : null;
            _ownActorId = actor ? actor.ActorId : 0;
            _nextRosterAt = 0f;
        }

        private void ResetState()
        {
            Close();
            _isPending = false;
            _wasRefusedAsMafia = false;
            _fakeTarget = 0;
            _fakeRemaining = VoteService.MaxVoteCount;
        }

        private void SetNotice(string message, Color color)
        {
            _notice.text = message;
            _notice.color = color;
        }

        // Stand-in name until the player side has nicknames (2026-10-15).
        private static string GetDisplayName(uint actorId)
        {
            return $"플레이어 {actorId:00}";
        }

        // Same stand-in input lock as the lighthouse screens until the player side has one.
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
