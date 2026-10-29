using System.Collections.Generic;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Player.Abilities.Net;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.IdentityCheck.View
{
    // The public announcement of a failed identity check (2026-10-29 sketch): the revealed citizen's job picture, name
    // and an "innocent citizen" stamp on a brush strip. Every client sees it, the mafia included, because the reveal is
    // public (win-vote-decisions 7). It never takes input and leaves on its own; reveals that land together queue up.
    [DisallowMultipleComponent]
    public sealed class InnocentRevealBanner : MonoBehaviour
    {
        // Timeline in seconds from the start of one banner.
        private const float EnterSeconds = 0.25f;
        private const float FrameStart = 0f;
        private const float FrameSeconds = 0.35f;
        private const float StripStart = 0.22f;
        private const float StripSeconds = 0.45f;
        private const float TitleStart = 0.45f;
        private const float NameStart = 0.58f;
        private const float MessageStart = 1.1f;
        private const float TextSeconds = 0.35f;
        private const float BadgeStart = 0.85f;
        private const float BadgeSeconds = 0.22f;
        private const float ShakeSeconds = 0.18f;
        private const float SparkleStart = 0.3f;
        private const float SlideDistance = 18f;

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;

        [Header("Parts")]
        [SerializeField]
        private RectTransform _banner;
        [SerializeField]
        private CanvasGroup _bannerGroup;
        [SerializeField]
        private RectTransform _frame;
        [SerializeField]
        private CanvasGroup _frameGroup;
        [SerializeField]
        private Image _portrait;
        [SerializeField]
        private Image _strip;
        [SerializeField]
        private CanvasGroup _title;
        [SerializeField]
        private Text _name;
        [SerializeField]
        private CanvasGroup _nameGroup;
        [SerializeField]
        private CanvasGroup _message;
        [SerializeField]
        private RectTransform _badge;
        [SerializeField]
        private CanvasGroup _badgeGroup;
        [SerializeField]
        private RectTransform[] _sparkles = new RectTransform[0];

        [Header("Layout")]
        [Tooltip("The badge follows the name by this gap, so a short name does not leave a hole.")]
        [SerializeField]
        private float _badgeGap = 24f;
        [Tooltip("A longer name shrinks to this width; the badge never moves past it.")]
        [SerializeField]
        private float _nameMaxWidth = 280f;

        [Header("Timing")]
        [SerializeField]
        private float _holdSeconds = 4.5f;
        [SerializeField]
        private float _exitSeconds = 0.4f;
        [SerializeField]
        private float _badgeStampScale = 1.8f;
        [SerializeField]
        private float _shakeDistance = 5f;

        [Header("Job pictures")]
        [SerializeField]
        private Sprite _navigatorPortrait;
        [SerializeField]
        private Sprite _mechanicPortrait;
        [SerializeField]
        private Sprite _guardPortrait;
        [SerializeField]
        private Sprite _gathererPortrait;

        private readonly Queue<uint> _pending = new Queue<uint>();
        private Vector2 _bannerPosition;
        private Vector2 _titlePosition;
        private Vector2 _namePosition;
        private Vector2 _messagePosition;
        private Vector2 _badgePosition;
        private float _elapsed;
        private bool _isShowing;

        private float TotalSeconds => _holdSeconds + _exitSeconds;

        private void Awake()
        {
            _bannerPosition = _banner.anchoredPosition;
            _titlePosition = ((RectTransform)_title.transform).anchoredPosition;
            _namePosition = ((RectTransform)_nameGroup.transform).anchoredPosition;
            _messagePosition = ((RectTransform)_message.transform).anchoredPosition;
            _badgePosition = _badge.anchoredPosition;
            _banner.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>(FindObjectsInactive.Include);
            }

            if (_voting)
            {
                _voting.InnocentCitizenRevealed += HandleCitizenRevealed;
            }
        }

        private void Update()
        {
            if (!_isShowing)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                Begin(_pending.Dequeue());
            }

            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= TotalSeconds)
            {
                Hide();
                return;
            }

            Animate(_elapsed);
        }

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.InnocentCitizenRevealed -= HandleCitizenRevealed;
            }

            _pending.Clear();
            Hide();
        }

        private void HandleCitizenRevealed(uint actorId)
        {
            _pending.Enqueue(actorId);
        }

        private void Begin(uint actorId)
        {
            NetworkActor actor = FindActor(actorId);
            _name.text = actor ? actor.DisplayName : $"플레이어 {actorId}";
            Sprite portrait = GetPortrait(actor);
            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait);

            float nameWidth = Mathf.Min(_name.preferredWidth, _nameMaxWidth);
            _badgePosition.x = _namePosition.x + nameWidth + _badgeGap + _badge.sizeDelta.x * _badge.pivot.x;

            _elapsed = 0f;
            _isShowing = true;
            _banner.gameObject.SetActive(true);
            Animate(0f);
        }

        private void Hide()
        {
            _isShowing = false;
            _banner.gameObject.SetActive(false);
        }

        // Frame pops in, the strip is painted out from behind it, the words slide in, and the badge is stamped down
        // with a short shake. The whole banner drops in at the start and rises out at the end.
        private void Animate(float time)
        {
            float enter = EaseOutCubic(Phase(time, 0f, EnterSeconds));
            float exit = Phase(time, _holdSeconds, _exitSeconds);
            _bannerGroup.alpha = enter * (1f - exit);
            Vector2 offset = new Vector2(Shake(time), SlideDistance * (1f - enter) + SlideDistance * EaseInCubic(exit));
            _banner.anchoredPosition = _bannerPosition + offset;

            float frame = Phase(time, FrameStart, FrameSeconds);
            _frameGroup.alpha = Mathf.Clamp01(frame * 3f);
            _frame.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(frame));

            _strip.fillAmount = EaseOutCubic(Phase(time, StripStart, StripSeconds));
            SlideIn(_title, _titlePosition, Phase(time, TitleStart, TextSeconds));
            SlideIn(_nameGroup, _namePosition, Phase(time, NameStart, TextSeconds));
            SlideIn(_message, _messagePosition, Phase(time, MessageStart, TextSeconds));

            float badge = Phase(time, BadgeStart, BadgeSeconds);
            _badgeGroup.alpha = Mathf.Clamp01(badge * 2f);
            _badge.anchoredPosition = _badgePosition;
            _badge.localScale = Vector3.one * Mathf.Lerp(_badgeStampScale, 1f, EaseInCubic(badge));

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Twinkle(_sparkles[i], time, i);
            }
        }

        private static void SlideIn(CanvasGroup group, Vector2 position, float progress)
        {
            float eased = EaseOutCubic(progress);
            group.alpha = eased;
            ((RectTransform)group.transform).anchoredPosition = position + new Vector2(-SlideDistance * (1f - eased), 0f);
        }

        // A short sideways jolt as the badge lands.
        private float Shake(float time)
        {
            float left = 1f - Phase(time, BadgeStart + BadgeSeconds, ShakeSeconds);
            return left <= 0f || left >= 1f ? 0f : Mathf.Sin(time * 90f) * _shakeDistance * left;
        }

        // Each sparkle pops in after the frame lands, then breathes and turns a little out of step with the others.
        private static void Twinkle(RectTransform sparkle, float time, int index)
        {
            float pop = EaseOutBack(Phase(time, SparkleStart + index * 0.12f, 0.3f));
            float breath = 0.8f + 0.25f * Mathf.Sin(time * 5f + index * 1.7f);
            sparkle.localScale = Vector3.one * (pop * breath);
            sparkle.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 2f + index) * 12f);
        }

        private static float Phase(float time, float start, float duration)
        {
            return Mathf.Clamp01((time - start) / duration);
        }

        private static float EaseOutCubic(float t)
        {
            float left = 1f - t;
            return 1f - left * left * left;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float left = t - 1f;
            return 1f + left * left * ((overshoot + 1f) * left + overshoot);
        }

        private static NetworkActor FindActor(uint actorId)
        {
            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity && identity.TryGetComponent(out NetworkActor actor) && actor.ActorId == actorId)
                {
                    return actor;
                }
            }

            return null;
        }

        // Same job lookup as the vote cards: each job prefab carries its own active ability request.
        private Sprite GetPortrait(NetworkActor actor)
        {
            if (!actor)
            {
                return null;
            }

            return actor.TryGetComponent(out NavigatorRouteRequest _) ? _navigatorPortrait
                : actor.TryGetComponent(out EmergencyRepairRequest _) ? _mechanicPortrait
                : actor.TryGetComponent(out GuardShieldRequest _) ? _guardPortrait
                : actor.TryGetComponent(out GatheringFeverRequest _) ? _gathererPortrait
                : null;
        }
    }
}
