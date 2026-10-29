using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Common.UI
{
    // Shared top-of-screen notice strip (2026-10-26). Any feature calls ScreenNotice.Show and the line queues, fades in,
    // holds and fades out; an optional small gauge fills and a gain floats up beside it. It draws on this machine only:
    // who sees a line is the caller's choice (call it on every machine for public news, only on the owner's for private
    // news), so secrecy rules stay with each feature. The strip placed in the scene registers itself while enabled; with
    // none, Show does nothing. With two enabled, the later one wins.
    [DisallowMultipleComponent]
    public sealed class ScreenNotice : MonoBehaviour
    {
        private const int Capacity = 8;

        private static ScreenNotice _active;

        [SerializeField]
        private CanvasGroup _group;
        [SerializeField]
        private Image _backing;
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _text;
        [SerializeField]
        private GameObject _gauge;
        [SerializeField]
        private Image _gaugeFill;
        [SerializeField]
        private Text _gain;
        [Tooltip("Used when a request brings no icon.")]
        [SerializeField]
        private Sprite _defaultIcon;
        [SerializeField, Min(0.5f)]
        private float _seconds = 2.6f;

        [Header("Styles")]
        [SerializeField]
        private Color _normalText = new Color(0.96f, 0.89f, 0.74f);
        [SerializeField]
        private Color _goldText = new Color(1f, 0.82f, 0.35f);
        [SerializeField]
        private Color _warningText = new Color(1f, 0.55f, 0.48f);
        [SerializeField]
        private Color _normalBacking = new Color(0.04f, 0.06f, 0.09f, 0.72f);
        [SerializeField]
        private Color _warningBacking = new Color(0.22f, 0.04f, 0.04f, 0.78f);
        [Tooltip("The gauge glints toward this as it fills; a gold line flashes it.")]
        [SerializeField]
        private Color _glintColor = new Color(1f, 0.95f, 0.6f);

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly ScreenNoticeRequest[] _queue = new ScreenNoticeRequest[Capacity];
        private int _head;
        private int _count;
        private float _timer;
        private ScreenNoticeRequest _current;
        private Color _fillColor;
        private RectTransform _textRect;
        private Vector2 _textWithGauge;
        private Vector2 _textAlone;
        private RectTransform _gainRect;
        private Vector2 _gainPosition;

        public static bool IsAvailable => _active;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _fillColor = _gaugeFill ? _gaugeFill.color : Color.white;

            // Without a gauge the text moves down to the strip's middle.
            if (_text)
            {
                _textRect = _text.rectTransform;
                _textWithGauge = _textRect.anchoredPosition;
                RectTransform strip = (RectTransform)_group.transform;
                _textAlone = new Vector2(_textWithGauge.x, -(strip.rect.height - _textRect.rect.height) * 0.5f);
            }

            if (_gain)
            {
                _gainRect = _gain.rectTransform;
                _gainPosition = _gainRect.anchoredPosition;
            }

            _group.alpha = 0f;
        }

        private void OnEnable()
        {
            _active = this;
        }

        private void LateUpdate()
        {
            if (_timer <= 0f)
            {
                if (_count == 0)
                {
                    return;
                }

                _current = _queue[_head];
                _queue[_head] = default;
                _head = (_head + 1) % Capacity;
                _count--;
                _timer = _seconds;
                Begin();
            }

            _timer = Mathf.Max(0f, _timer - Time.unscaledDeltaTime);
            float elapsed = _seconds - _timer;
            // Fades in fast, out a little slower, and the next line waits for it.
            _group.alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.25f), Mathf.Clamp01(_timer / 0.5f));
            AnimateGauge(elapsed);
            AnimateGain(elapsed);
        }

        private void OnDisable()
        {
            if (_active == this)
            {
                _active = null;
            }
        }

        // Queues a line on this machine's screen. False when no strip is in the scene.
        public static bool Show(in ScreenNoticeRequest request)
        {
            if (!_active || string.IsNullOrEmpty(request.Text))
            {
                return false;
            }

            _active.Enqueue(request);
            return true;
        }

        // Drops the waiting lines and the one showing, e.g. when the match ends.
        public static void Clear()
        {
            if (_active)
            {
                _active._count = 0;
                _active._timer = 0f;
                _active._group.alpha = 0f;
            }
        }

        private void Enqueue(in ScreenNoticeRequest request)
        {
            // A full queue drops the oldest waiting line.
            if (_count == Capacity)
            {
                _head = (_head + 1) % Capacity;
                _count--;
            }

            _queue[(_head + _count) % Capacity] = request;
            _count++;
        }

        private void Begin()
        {
            bool isWarning = _current.Style == ScreenNoticeStyle.Warning;

            if (_text)
            {
                _text.text = _current.Text;
                _text.color = _current.Style == ScreenNoticeStyle.Gold ? _goldText : isWarning ? _warningText : _normalText;
                _textRect.anchoredPosition = _current.HasGauge ? _textWithGauge : _textAlone;
            }

            if (_backing)
            {
                _backing.color = isWarning ? _warningBacking : _normalBacking;
            }

            if (_icon)
            {
                Sprite icon = _current.Icon ? _current.Icon : _defaultIcon;
                _icon.sprite = icon;
                _icon.enabled = icon;
            }

            if (_gauge)
            {
                _gauge.SetActive(_current.HasGauge);
            }

            if (_gaugeFill)
            {
                _gaugeFill.fillAmount = _current.GaugeFrom;
                _gaugeFill.color = _fillColor;
            }

            if (_gain)
            {
                _gain.enabled = _current.HasGauge && !string.IsNullOrEmpty(_current.Gain);
                _gain.text = _current.Gain ?? string.Empty;
            }
        }

        private void AnimateGauge(float elapsed)
        {
            if (!_gaugeFill || !_current.HasGauge)
            {
                return;
            }

            float fill = Mathf.Clamp01((elapsed - 0.2f) / 0.7f);
            _gaugeFill.fillAmount = Mathf.Lerp(_current.GaugeFrom, _current.GaugeTo, 1f - (1f - fill) * (1f - fill));
            // A glint while it fills; a gold line flashes a few times instead.
            float glint = _current.Style == ScreenNoticeStyle.Gold
                ? Mathf.PingPong(elapsed * 3f, 1f) * Mathf.Clamp01(1.4f - elapsed)
                : Mathf.Sin(fill * Mathf.PI) * 0.6f;
            _gaugeFill.color = Color.Lerp(_fillColor, _glintColor, glint);
        }

        private void AnimateGain(float elapsed)
        {
            if (!_gain || !_gain.enabled)
            {
                return;
            }

            float rise = Mathf.Clamp01((elapsed - 0.3f) / 1.2f);
            _gainRect.anchoredPosition = _gainPosition + new Vector2(0f, 26f * rise);
            Color color = _gain.color;
            color.a = rise > 0f ? 1f - rise * rise : 0f;
            _gain.color = color;
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
