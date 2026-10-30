using System.Collections.Generic;
using Lighthouse.Common.UI;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.View;
using Lighthouse.Player.Abilities.Net;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Mafia.Voting.View
{
    // Secret vote UI (2026-10-22 sketch): an in-game button that shows the own votes left, the vote screen with one card
    // per other player, and the confirm box asked before a vote is spent (2026-10-29: the box again, not text on the
    // card). The server decides every vote; this screen only asks. The mafia cannot vote: it sees the same cards greyed
    // with no button to press, and a notice says why each time the screen opens (2026-10-30, replaces the pretend count).
    [DisallowMultipleComponent]
    public sealed class VotingScreen : MonoBehaviour
    {
        private const float RosterRefreshSeconds = 1f;
        private const string Highlight = "#F6C64A";
        private const float BallotDropSeconds = 0.25f;
        private const float BallotDropDistance = 14f;
        private const float BallotDropAngle = -12f;
        private const float NoticeFadeSeconds = 0.4f;
        private const float BlinkSeconds = 0.8f;
        private const float LockFadeSeconds = 0.15f;
        private const string MafiaCannotVoteText = "마피아는 투표할 수 없습니다";

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private Key _openKey = Key.V;
        [SerializeField]
        private float _fadeInSeconds = 0.12f;
        [Tooltip("How long a notice stays before it fades out.")]
        [SerializeField]
        private float _noticeHoldSeconds = 2.5f;
        [Tooltip("The last vote in the confirm box and the in-game count when one is left.")]
        [SerializeField]
        private Color _lastChanceColor = new Color(0.94f, 0.54f, 0.24f);

        [Header("In-game button")]
        [SerializeField]
        private GameObject _hud;
        [SerializeField]
        private Button _hudButton;
        [SerializeField]
        private TMP_Text _hudRemaining;
        [SerializeField]
        private TMP_Text _hudTarget;

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
        [Tooltip("Shown over the cards once the ritual locks the votes. Optional.")]
        [SerializeField]
        private GameObject _lockBand;

        // The job shows through the player prefab each job spawns as (2026-10-27), so every client already knows it.
        [Header("Job portraits")]
        [SerializeField]
        private Sprite _navigatorPortrait;
        [SerializeField]
        private Sprite _mechanicPortrait;
        [SerializeField]
        private Sprite _guardPortrait;
        [SerializeField]
        private Sprite _gathererPortrait;

        [Header("Confirm box")]
        [SerializeField]
        private GameObject _confirm;
        [SerializeField]
        private Text _confirmTarget;
        [SerializeField]
        private Text _confirmCount;
        [SerializeField]
        private Button _confirmButton;
        [SerializeField]
        private Button _cancelButton;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<uint> _candidates = new List<uint>();
        // Every spawned player but this one, filled with the candidates; names and jobs are read from here.
        private readonly Dictionary<uint, NetworkActor> _actors = new Dictionary<uint, NetworkActor>();
        private CanvasGroup _windowGroup;
        private float _openedAt;
        private NetworkIdentity _player;
        private PlayerInput _playerInput;
        private uint _ownActorId;
        private float _nextRosterAt;
        private bool _isOpen;
        private bool _isPending;
        private uint _pendingTarget;
        private uint _requestedTarget;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        // Seen when the server refuses a vote as the mafia's, in case the role message has not arrived.
        private bool _wasRefusedAsMafia;
        // Used ballots last drawn, so a newly spent one drops; -1 until the open screen has drawn once.
        private int _shownUsed = -1;
        private int _droppingBallot = -1;
        private float _dropEndsAt;
        private Vector2 _dropRest;
        private Color _noticeBase;
        private float _noticeAt;
        private float _noticeShakeEndsAt;
        private Vector2 _noticeRest;
        private CanvasGroup _lockBandGroup;
        // The band also tells the mafia it cannot vote; its own caption comes back once the ritual locks the votes.
        private Text _lockBandText;
        private string _lockBandCaption;
        private float _lockShownAt;
        private Color _hudRemainingColor;
        private Color _hudTargetColor;
        // The count the in-game button last showed; it blinks when it comes back down to the last vote.
        private int _hudShownRemaining = -1;
        private float _remainingBlinkEndsAt;
        private float _lockBlinkEndsAt;
        private bool _hasLockState;
        private bool _wasLocked;
        private bool _isLockBlinkWaiting;

        public bool IsOpen => _isOpen;

        // Only the player's own role is sent to a client (2026-10-22); identity state also reaches the mafia alone.
        private bool IsOwnMafia => (LighthouseNetworkManager.Instance && LighthouseNetworkManager.Instance.OwnRole == MatchRole.Mafia)
            || (_voting && _voting.HasOwnIdentityState) || _wasRefusedAsMafia;

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

            if (_lockBand)
            {
                if (!_lockBand.TryGetComponent(out _lockBandGroup))
                {
                    _lockBandGroup = _lockBand.AddComponent<CanvasGroup>();
                }

                _lockBandText = _lockBand.GetComponentInChildren<Text>(true);
                _lockBandCaption = _lockBandText ? _lockBandText.text : string.Empty;
                _lockBand.SetActive(false);
            }

            _noticeRest = _notice.rectTransform.anchoredPosition;
            _hudRemainingColor = _hudRemaining.color;
            _hudTargetColor = _hudTarget.color;
            _hudButton.onClick.AddListener(Open);
            _confirmButton.onClick.AddListener(HandleConfirmClicked);
            _cancelButton.onClick.AddListener(CloseConfirm);
            _window.SetActive(false);
            _confirm.SetActive(false);
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
            AnimateBallot();
            AnimateNotice();
            AnimateLockBand();
            Blink(_hudRemaining, _hudRemainingColor, _lastChanceColor, ref _remainingBlinkEndsAt);
            Blink(_hudTarget, _hudTargetColor, _rejectColor, ref _lockBlinkEndsAt);

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
            _confirm.SetActive(false);
            _pendingTarget = 0;
            _shownUsed = -1;
            SetNotice(string.Empty, _noticeColor);
            LockPlayer(true);
            RefreshRoster();
            Refresh();

            if (IsOwnMafia)
            {
                ScreenNotice.Show(new ScreenNoticeRequest(MafiaCannotVoteText, ScreenNoticeStyle.Warning));
            }
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _pendingTarget = 0;
            StopBallotDrop();
            StopNoticeShake();
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _window.SetActive(false);
            _confirm.SetActive(false);
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

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame)
            {
                Close();
                return;
            }

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
            uint current = vote.TargetActorId;
            // The server already counts none for the mafia.
            int remaining = vote.RemainingVoteCount;
            bool isLocked = vote.IsLocked || vote.IsEnded;
            TrackLock(isLocked);

            if (isLocked && _pendingTarget != 0)
            {
                CloseConfirm();
                SetNotice("점화 의식이 시작되어 투표가 잠겼습니다.", _rejectColor);
                ShakeNotice();
            }

            _hudRemaining.text = isMafia ? "투표권 없음" : $"남은 지목  {remaining} / {VoteService.MaxVoteCount}";
            _hudTarget.text = isLocked ? "투표 잠김"
                : isMafia ? "지목할 수 없음"
                : current != 0 ? $"내 지목: {GetDisplayName(current)}" : "내 지목 없음";
            TrackHudBlinks(remaining);
            int used = VoteService.MaxVoteCount - remaining;

            if (!_isOpen)
            {
                return;
            }

            if (_shownUsed >= 0 && used > _shownUsed && used <= _ballots.Length)
            {
                StartBallotDrop(used - 1);
            }

            _shownUsed = used;

            for (int i = 0; i < _ballots.Length; i++)
            {
                if (i != _droppingBallot)
                {
                    _ballots[i].sprite = i < used ? _ballotUsed : _ballotLeft;
                }
            }

            // The mafia's ballots all show as spent and its count reads zero in red, beside the band that says why.
            _remaining.text = isMafia
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(_rejectColor)}>0</color> / {VoteService.MaxVoteCount}"
                : $"<color={Highlight}>{remaining}</color> / {VoteService.MaxVoteCount}";
            ShowLockBand(isLocked || isMafia, isLocked ? _lockBandCaption : MafiaCannotVoteText);
            bool canPick = !isMafia && !isLocked && !_isPending && _pendingTarget == 0;

            for (int i = 0; i < _cards.Length; i++)
            {
                bool hasCandidate = i < _candidates.Count;
                _cards[i].gameObject.SetActive(hasCandidate);

                if (hasCandidate)
                {
                    uint actorId = _candidates[i];
                    _cards[i].Show(actorId, GetDisplayName(actorId), GetPortrait(actorId), actorId == current, canPick,
                        isLocked || isMafia);
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
            uint current = vote.TargetActorId;
            int remaining = vote.RemainingVoteCount;

            // Re-picking the current target is free and needs no box; an empty count only says so.
            if (card.ActorId == current)
            {
                SetNotice("이미 지목하고 있는 선원입니다.", _noticeColor);
            }
            else if (remaining <= 0)
            {
                SetNotice("남은 지목 기회가 없습니다.", _rejectColor);
                ShakeNotice();
                card.Shake();
            }
            else
            {
                SetNotice(string.Empty, _noticeColor);
                _pendingTarget = card.ActorId;
                _confirmTarget.text = $"<color={Highlight}>{GetDisplayName(card.ActorId)}</color>님을\n지목하시겠습니까?";
                _confirmCount.text = remaining == 1
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(_lastChanceColor)}>마지막 기회 1회 → 0회</color>"
                    : $"남은 기회 {remaining}회 → <color={Highlight}>{remaining - 1}회</color>";
                _confirm.SetActive(true);
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
            _isPending = true;
            _requestedTarget = target;
            _voting.RequestVote(target);
        }

        private void CloseConfirm()
        {
            _pendingTarget = 0;
            _confirm.SetActive(false);
        }

        // The server has the last word: a refused vote closes the box and says why.
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
                    // Only a vote sent before the role arrived gets here; the cards grey out from now on.
                    _wasRefusedAsMafia = true;
                    CloseConfirm();
                    SetNotice($"{MafiaCannotVoteText}.", _rejectColor);
                    ShakeRefused();
                    break;
                case VoteChangeResult.VotingLocked:
                case VoteChangeResult.GameEnded:
                    CloseConfirm();
                    SetNotice("점화 의식이 시작되어 투표가 잠겼습니다.", _rejectColor);
                    ShakeRefused();
                    break;
                case VoteChangeResult.NoVotesRemaining:
                    CloseConfirm();
                    SetNotice("남은 지목 기회가 없습니다.", _rejectColor);
                    ShakeRefused();
                    break;
                default:
                    CloseConfirm();
                    SetNotice("지목하지 못했습니다. 잠시 후 다시 시도하세요.", _rejectColor);
                    ShakeRefused();
                    break;
            }
        }

        private void ShakeRefused()
        {
            ShakeNotice();

            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i].gameObject.activeSelf && _cards[i].ActorId == _requestedTarget)
                {
                    _cards[i].Shake();
                }
            }
        }

        // The lock seen while playing; joining a match that is already locked does not blink.
        private void TrackLock(bool isLocked)
        {
            if (_hasLockState && isLocked && !_wasLocked)
            {
                _isLockBlinkWaiting = true;
            }

            _hasLockState = true;
            _wasLocked = isLocked;
        }

        // The button is hidden while the screen is open, so its blinks wait until it shows again.
        private void TrackHudBlinks(int remaining)
        {
            if (!_hud.activeSelf)
            {
                return;
            }

            if (_hudShownRemaining >= 0 && remaining < _hudShownRemaining && remaining == 1)
            {
                _remainingBlinkEndsAt = Time.unscaledTime + BlinkSeconds;
            }

            _hudShownRemaining = remaining;

            if (_isLockBlinkWaiting)
            {
                _isLockBlinkWaiting = false;
                _lockBlinkEndsAt = Time.unscaledTime + BlinkSeconds;
            }
        }

        private void ShowLockBand(bool isShown, string caption)
        {
            if (!_lockBand)
            {
                return;
            }

            // A new caption fades in again, e.g. when the ritual locks the votes over the mafia's band.
            if (_lockBandText && isShown && _lockBandText.text != caption)
            {
                _lockBandText.text = caption;
                _lockShownAt = Time.unscaledTime;
                _lockBandGroup.alpha = 0f;
            }

            if (_lockBand.activeSelf == isShown)
            {
                return;
            }

            _lockBand.SetActive(isShown);
            _lockShownAt = Time.unscaledTime;
            _lockBandGroup.alpha = 0f;
        }

        private void AnimateLockBand()
        {
            if (_lockBand && _lockBand.activeSelf && _lockBandGroup.alpha < 1f)
            {
                _lockBandGroup.alpha = Mathf.Clamp01((Time.unscaledTime - _lockShownAt) / LockFadeSeconds);
            }
        }

        // The spent ballot tips over and sinks, then turns into the used one.
        private void StartBallotDrop(int index)
        {
            StopBallotDrop();
            _droppingBallot = index;
            _dropRest = _ballots[index].rectTransform.anchoredPosition;
            _dropEndsAt = Time.unscaledTime + BallotDropSeconds;
            _ballots[index].sprite = _ballotLeft;
        }

        private void AnimateBallot()
        {
            if (_droppingBallot < 0)
            {
                return;
            }

            float left = _dropEndsAt - Time.unscaledTime;

            if (left <= 0f)
            {
                StopBallotDrop();
                return;
            }

            float progress = 1f - left / BallotDropSeconds;
            Image ballot = _ballots[_droppingBallot];
            ballot.rectTransform.anchoredPosition = _dropRest + Vector2.down * (BallotDropDistance * progress);
            ballot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, BallotDropAngle * progress);
            Color color = ballot.color;
            color.a = 1f - 0.6f * progress;
            ballot.color = color;
        }

        private void StopBallotDrop()
        {
            if (_droppingBallot < 0)
            {
                return;
            }

            Image ballot = _ballots[_droppingBallot];
            ballot.rectTransform.anchoredPosition = _dropRest;
            ballot.rectTransform.localRotation = Quaternion.identity;
            Color color = ballot.color;
            color.a = 1f;
            ballot.color = color;
            ballot.sprite = _ballotUsed;
            _droppingBallot = -1;
        }

        private void ShakeNotice()
        {
            _noticeShakeEndsAt = Time.unscaledTime + VoteCandidateCard.ShakeSeconds;
        }

        private void AnimateNotice()
        {
            float now = Time.unscaledTime;

            if (_noticeAt > 0f)
            {
                float faded = (now - _noticeAt - _noticeHoldSeconds) / NoticeFadeSeconds;

                if (faded > 0f)
                {
                    Color color = _noticeBase;
                    color.a *= 1f - Mathf.Clamp01(faded);
                    _notice.color = color;

                    if (faded >= 1f)
                    {
                        _noticeAt = 0f;
                    }
                }
            }

            if (_noticeShakeEndsAt <= 0f)
            {
                return;
            }

            float left = _noticeShakeEndsAt - now;

            if (left <= 0f)
            {
                StopNoticeShake();
                return;
            }

            _notice.rectTransform.anchoredPosition = _noticeRest + new Vector2(VoteCandidateCard.ShakeOffset(left), 0f);
        }

        private void StopNoticeShake()
        {
            if (_noticeShakeEndsAt <= 0f)
            {
                return;
            }

            _noticeShakeEndsAt = 0f;
            _notice.rectTransform.anchoredPosition = _noticeRest;
        }

        // Two quick flashes towards the flash colour and back; the text is left alone between blinks.
        private static void Blink(TMP_Text text, Color rest, Color flash, ref float endsAt)
        {
            if (endsAt <= 0f)
            {
                return;
            }

            float left = endsAt - Time.unscaledTime;

            if (left <= 0f)
            {
                endsAt = 0f;
                text.color = rest;
                return;
            }

            float flashes = Mathf.Abs(Mathf.Sin((1f - left / BlinkSeconds) * 2f * Mathf.PI));
            text.color = Color.Lerp(rest, flash, flashes);
        }

        private void RefreshRoster()
        {
            _nextRosterAt = Time.unscaledTime + RosterRefreshSeconds;
            _candidates.Clear();
            _actors.Clear();

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity && identity.TryGetComponent(out NetworkActor actor)
                    && actor.ActorId != 0 && actor.ActorId != _ownActorId && !_candidates.Contains(actor.ActorId))
                {
                    _candidates.Add(actor.ActorId);
                    _actors[actor.ActorId] = actor;
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
            _hasLockState = false;
            _isLockBlinkWaiting = false;
            _hudShownRemaining = -1;

            if (_remainingBlinkEndsAt > 0f || _lockBlinkEndsAt > 0f)
            {
                _remainingBlinkEndsAt = 0f;
                _lockBlinkEndsAt = 0f;
                _hudRemaining.color = _hudRemainingColor;
                _hudTarget.color = _hudTargetColor;
            }
        }

        private void SetNotice(string message, Color color)
        {
            _notice.text = message;
            _notice.color = color;
            _noticeBase = color;
            _noticeAt = string.IsNullOrEmpty(message) ? 0f : Time.unscaledTime;
        }

        // The lobby nickname the server carried over; the same fallback as the lobby if the player is not seen yet.
        private string GetDisplayName(uint actorId)
        {
            return _actors.TryGetValue(actorId, out NetworkActor actor) && actor
                ? actor.DisplayName
                : $"플레이어 {actorId}";
        }

        // Each job prefab carries its own active ability request (Captain/Tech/Tanker/Farm 1), so that names the job.
        private Sprite GetPortrait(uint actorId)
        {
            if (!_actors.TryGetValue(actorId, out NetworkActor actor) || !actor)
            {
                return null;
            }

            return actor.TryGetComponent(out NavigatorRouteRequest _) ? _navigatorPortrait
                : actor.TryGetComponent(out EmergencyRepairRequest _) ? _mechanicPortrait
                : actor.TryGetComponent(out GuardShieldRequest _) ? _guardPortrait
                : actor.TryGetComponent(out GatheringFeverRequest _) ? _gathererPortrait
                : null;
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
