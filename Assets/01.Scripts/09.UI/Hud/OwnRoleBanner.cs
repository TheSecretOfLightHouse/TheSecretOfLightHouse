using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Player.Abilities.Net;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // Tells the local player their own role once at match start (2026-10-31), in the failed identity check banner's
    // style: the same frame, brush strip, sparkles and timeline. The mafia's frame holds the workbench withdraw mask,
    // turned upright; a citizen's holds their job picture. Citizens get the same banner for the same time, so its
    // showing alone does not single out the mafia. It draws on this machine only, from the role the server sent this
    // player alone. Built at runtime under this RectTransform so no prefab changes; never takes input.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class OwnRoleBanner : MonoBehaviour
    {
        // The reveal banner sketch is 861x306; it hangs under the day board at the top centre, as the reveal banner does.
        private const float BannerWidth = 861f;
        private const float BannerHeight = 306f;
        private const float BustScale = 0.95f;

        // Timeline in seconds from the start, as in InnocentRevealBanner.
        private const float EnterSeconds = 0.25f;
        private const float FrameSeconds = 0.35f;
        private const float StripStart = 0.22f;
        private const float StripSeconds = 0.45f;
        private const float TitleStart = 0.45f;
        private const float NameStart = 0.58f;
        private const float MessageStart = 1.1f;
        private const float TextSeconds = 0.35f;
        private const float SparkleStart = 0.3f;
        private const float SlideDistance = 18f;

        private const string MafiaName = "마피아";
        private const string CitizenName = "선원";

        [Header("Sprites")]
        [SerializeField]
        private Sprite _frameSprite;
        [SerializeField]
        private Sprite _stripSprite;
        [SerializeField]
        private Sprite _portraitMaskSprite;
        [SerializeField]
        private Sprite _sparkleTopSprite;
        [SerializeField]
        private Sprite _sparkleBottomSprite;
        [Tooltip("The workbench withdraw button's mask, so the mafia's own things share one mark.")]
        [SerializeField]
        private Sprite _mafiaMask;
        [Tooltip("The mask is drawn tilted; this turns it level (degrees, counter-clockwise).")]
        [SerializeField]
        private float _mafiaMaskTilt = -19f;
        [SerializeField]
        private Sprite _navigatorPortrait;
        [SerializeField]
        private Sprite _mechanicPortrait;
        [SerializeField]
        private Sprite _guardPortrait;
        [SerializeField]
        private Sprite _gathererPortrait;

        [Header("Words")]
        [SerializeField]
        private string _title = "당신의 역할";
        [SerializeField]
        private string _mafiaMessage = "정체를 숨기고 등대가 다시 밝혀지는 것을 막으세요.";
        [SerializeField]
        private string _citizenMessage = "등대를 다시 밝히고 숨은 마피아를 찾아내세요.";

        [Header("Layout")]
        [SerializeField]
        private float _scale = 0.72f;
        [SerializeField]
        private float _top = 200f;

        [Header("Timing")]
        [Tooltip("Wait after the local player appears, so the scene load hitch does not eat the entrance.")]
        [SerializeField, Min(0f)]
        private float _delaySeconds = 1f;
        [SerializeField]
        private float _holdSeconds = 5f;
        [SerializeField]
        private float _exitSeconds = 0.4f;

        [Header("Colours")]
        [SerializeField]
        private Color _cream = new Color(0.98f, 0.92f, 0.79f);
        [SerializeField]
        private Color _sand = new Color(0.86f, 0.77f, 0.63f);
        [SerializeField]
        private Color _mafiaColor = new Color(1f, 0.42f, 0.36f);
        [SerializeField]
        private Color _outline = new Color(0.12f, 0.09f, 0.05f, 0.6f);

        [Header("Font")]
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private RectTransform _banner;
        private CanvasGroup _bannerGroup;
        private RectTransform _frame;
        private CanvasGroup _frameGroup;
        private Image _portrait;
        private Image _mask;
        private Image _strip;
        private CanvasGroup _titleGroup;
        private Text _name;
        private CanvasGroup _nameGroup;
        private Text _message;
        private CanvasGroup _messageGroup;
        private RectTransform[] _sparkles;
        private Vector2 _bannerPosition;
        private Vector2 _titlePosition;
        private Vector2 _namePosition;
        private Vector2 _messagePosition;
        private NetworkIdentity _shownFor;
        private float _startsAt;
        private float _elapsed;
        private bool _isWaiting;
        private bool _isShowing;

        private float TotalSeconds => _holdSeconds + _exitSeconds;

        private void Awake()
        {
            Build();
            _banner.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            TrackPlayer();

            if (_isWaiting && Time.unscaledTime >= _startsAt)
            {
                _isWaiting = false;
                Begin();
            }

            if (!_isShowing)
            {
                return;
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
            _isWaiting = false;
            Hide();
        }

        // Once per local player object: each match spawns a new one, and the role arrives with the match start, which
        // may be before or after the player object, so both are waited for.
        private void TrackPlayer()
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            if (!player || player == _shownFor)
            {
                return;
            }

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (!manager || manager.OwnRole == MatchRole.None)
            {
                return;
            }

            _shownFor = player;
            _startsAt = Time.unscaledTime + _delaySeconds;
            _isWaiting = true;
        }

        private void Begin()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (!manager || manager.OwnRole == MatchRole.None)
            {
                return;
            }

            bool isMafia = manager.OwnRole == MatchRole.Mafia;
            Sprite portrait = isMafia ? null : GetPortrait(_shownFor);
            _mask.gameObject.SetActive(isMafia);
            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait);
            _name.text = isMafia ? MafiaName : CitizenName;
            _name.color = isMafia ? _mafiaColor : _cream;
            _message.text = isMafia ? _mafiaMessage : _citizenMessage;

            _elapsed = 0f;
            _isShowing = true;
            _banner.gameObject.SetActive(true);
            Animate(0f);
        }

        private void Hide()
        {
            _isShowing = false;

            if (_banner)
            {
                _banner.gameObject.SetActive(false);
            }
        }

        // Same motion as the failed check banner: the frame pops in, the strip is painted out from behind it and the
        // words slide in; the whole banner drops in at the start and rises out at the end.
        private void Animate(float time)
        {
            float enter = EaseOutCubic(Phase(time, 0f, EnterSeconds));
            float exit = Phase(time, _holdSeconds, _exitSeconds);
            _bannerGroup.alpha = enter * (1f - exit);
            _banner.anchoredPosition = _bannerPosition
                + new Vector2(0f, SlideDistance * (1f - enter) + SlideDistance * EaseInCubic(exit));

            float frame = Phase(time, 0f, FrameSeconds);
            _frameGroup.alpha = Mathf.Clamp01(frame * 3f);
            _frame.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(frame));

            _strip.fillAmount = EaseOutCubic(Phase(time, StripStart, StripSeconds));
            SlideIn(_titleGroup, _titlePosition, Phase(time, TitleStart, TextSeconds));
            SlideIn(_nameGroup, _namePosition, Phase(time, NameStart, TextSeconds));
            SlideIn(_messageGroup, _messagePosition, Phase(time, MessageStart, TextSeconds));

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Twinkle(_sparkles[i], time, i);
            }
        }

        // Laid out in the sketch's own pixels with the reveal banner's numbers, then scaled down as one group.
        private void Build()
        {
            Font font = Font.CreateDynamicFontFromOSFont(_osFontNames, 32);

            _banner = Rect("RoleBanner", transform, 0f, 0f, BannerWidth, BannerHeight);
            _banner.anchorMin = _banner.anchorMax = _banner.pivot = new Vector2(0.5f, 1f);
            _banner.anchoredPosition = new Vector2(0f, -_top);
            _banner.localScale = Vector3.one * _scale;
            _bannerPosition = _banner.anchoredPosition;
            _bannerGroup = Fade(_banner);

            _strip = Picture("Strip", _banner, _stripSprite, 16f, 26f, 806f, 238f);
            _strip.type = Image.Type.Filled;
            _strip.fillMethod = Image.FillMethod.Horizontal;
            _strip.fillOrigin = (int)Image.OriginHorizontal.Left;

            Image frame = Picture("Frame", _banner, _frameSprite, 52f, 32f, 231f, 229f);
            _frame = frame.rectTransform;
            CenterPivot(_frame);
            _frameGroup = Fade(_frame);
            Image portraitMask = Picture("PortraitMask", _frame, _portraitMaskSprite, 18f, 18f, 195f, 193f);
            portraitMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _portrait = Picture("Portrait", portraitMask.transform, null, 0f, 0f, 195f * BustScale, 195f * BustScale);
            RectTransform bust = _portrait.rectTransform;
            bust.anchorMin = bust.anchorMax = bust.pivot = new Vector2(0.5f, 0f);
            bust.anchoredPosition = Vector2.zero;
            _portrait.preserveAspect = true;

            _mask = Picture("MafiaMask", _frame, _mafiaMask, 0f, 0f, 150f, 150f);
            RectTransform maskRect = _mask.rectTransform;
            maskRect.anchorMin = maskRect.anchorMax = maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = Vector2.zero;
            maskRect.localEulerAngles = new Vector3(0f, 0f, _mafiaMaskTilt);
            _mask.preserveAspect = true;

            Text title = Label("Title", _banner, font, _title, 24, 312f, 82f, 320f, 34f);
            title.color = _sand;
            _titleGroup = Fade(title.rectTransform);
            _titlePosition = title.rectTransform.anchoredPosition;
            _name = Label("Name", _banner, font, string.Empty, 52, 308f, 114f, 440f, 70f);
            _nameGroup = Fade(_name.rectTransform);
            _namePosition = _name.rectTransform.anchoredPosition;
            _message = Label("Message", _banner, font, string.Empty, 26, 312f, 186f, 480f, 38f);
            _message.resizeTextForBestFit = true;
            _message.resizeTextMinSize = 18;
            _message.resizeTextMaxSize = 26;
            _message.verticalOverflow = VerticalWrapMode.Truncate;
            _messageGroup = Fade(_message.rectTransform);
            _messagePosition = _message.rectTransform.anchoredPosition;

            Image sparkleTop = Picture("SparkleTop", _banner, _sparkleTopSprite, 276f, 20f, 30f, 40f);
            CenterPivot(sparkleTop.rectTransform);
            Image sparkleBottom = Picture("SparkleBottom", _banner, _sparkleBottomSprite, 274f, 214f, 32f, 50f);
            CenterPivot(sparkleBottom.rectTransform);
            _sparkles = new[] { sparkleTop.rectTransform, sparkleBottom.rectTransform };
        }

        // Same job lookup as the HUD and vote cards: each job prefab carries its own active ability request.
        private Sprite GetPortrait(NetworkIdentity player)
        {
            if (!player)
            {
                return null;
            }

            return player.TryGetComponent(out NavigatorRouteRequest _) ? _navigatorPortrait
                : player.TryGetComponent(out EmergencyRepairRequest _) ? _mechanicPortrait
                : player.TryGetComponent(out GuardShieldRequest _) ? _guardPortrait
                : player.TryGetComponent(out GatheringFeverRequest _) ? _gathererPortrait
                : null;
        }

        private Text Label(string name, Transform parent, Font font, string caption, int size, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = _cream;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = caption;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = _outline;
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static CanvasGroup Fade(RectTransform rect)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        // Keeps the place drawn in the sketch while letting the part scale around its own centre.
        private static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
        }

        private static void SlideIn(CanvasGroup group, Vector2 position, float progress)
        {
            float eased = EaseOutCubic(progress);
            group.alpha = eased;
            ((RectTransform)group.transform).anchoredPosition = position + new Vector2(-SlideDistance * (1f - eased), 0f);
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
    }
}
