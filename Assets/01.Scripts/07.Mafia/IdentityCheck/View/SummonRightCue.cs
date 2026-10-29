using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.IdentityCheck.View
{
    // Makes the mafia's 10 second summon right hard to miss (2026-10-29): a screen-edge flash and a short line when the
    // right is granted, a glow and pulse on the summon button with a bar that drains with the time, a red last stretch
    // with the seconds, and a line when it lapses or the summon goes through. Only the mafia receives identity state, so
    // citizens never see any of it. Nothing here takes input, so the right's seconds are never spent on a box.
    [DisallowMultipleComponent]
    public sealed class SummonRightCue : MonoBehaviour
    {
        private const float FlashSeconds = 0.9f;
        private const float LineFadeSeconds = 0.25f;
        private const int GlowTextureSize = 64;
        private const int GlowBorder = 24;
        private const int VignetteTextureSize = 128;

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;

        [Header("Parts")]
        [Tooltip("Needs a centre pivot so the pulse grows in place.")]
        [SerializeField]
        private RectTransform _summonButton;
        [SerializeField]
        private Image _glow;
        [SerializeField]
        private GameObject _timer;
        [SerializeField]
        private Image _timerFill;
        [SerializeField]
        private Text _seconds;
        [SerializeField]
        private Image _vignette;
        [SerializeField]
        private CanvasGroup _line;
        [SerializeField]
        private Text _lineText;
        [SerializeField]
        private AudioSource _audio;

        [Header("Look")]
        [SerializeField]
        private Color _readyColor = new Color(0.3f, 0.9f, 0.82f);
        [SerializeField]
        private Color _hurryColor = new Color(0.95f, 0.32f, 0.25f);
        [SerializeField]
        private Color _lapsedColor = new Color(0.66f, 0.62f, 0.55f);
        [Tooltip("The last seconds turn red, pulse faster and show the count.")]
        [SerializeField]
        private float _hurrySeconds = 3f;
        [SerializeField]
        private float _pulseScale = 0.08f;
        [Tooltip("Pulses per second.")]
        [SerializeField]
        private float _pulseRate = 1.2f;
        [SerializeField]
        private float _hurryPulseRate = 3f;
        [SerializeField]
        private float _lineSeconds = 1.8f;
        [Tooltip("Played on this client only when the right is granted. Empty = silent.")]
        [SerializeField]
        private AudioClip _grantedClip;

        private Texture2D _glowTexture;
        private Texture2D _vignetteTexture;
        private Sprite _glowSprite;
        private Sprite _vignetteSprite;
        private double _grantedExpiresAt = -1;
        private bool _wasLive;
        private bool _hasSummoned;
        private float _flashStartedAt = -1f;
        private float _lineStartedAt = -1f;
        private int _shownSeconds = -1;

        private void Awake()
        {
            CreateSprites();
            _glow.sprite = _glowSprite;
            _glow.type = Image.Type.Sliced;
            _vignette.sprite = _vignetteSprite;
            HideRight();
            _vignette.gameObject.SetActive(false);
            _line.gameObject.SetActive(false);
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

        private void OnDisable()
        {
            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
            }

            HideRight();
        }

        private void OnDestroy()
        {
            Destroy(_glowSprite);
            Destroy(_vignetteSprite);
            Destroy(_glowTexture);
            Destroy(_vignetteTexture);
        }

        private void Update()
        {
            if (_voting && _voting.HasOwnIdentityState)
            {
                RefreshRight(_voting.OwnIdentityState);
            }
            else
            {
                _hasSummoned = false;
                _wasLive = false;
                _grantedExpiresAt = -1;
                HideRight();
            }

            RefreshFlash();
            RefreshLine();
        }

        private void RefreshRight(in IdentityResponse state)
        {
            double left = state.SummonExpiresAt - NetworkTime.time;
            bool isLive = state.HasSummonRight && left > 0;

            // A new expiry is a new grant, so a second successful check plays the opening again.
            if (isLive && state.SummonExpiresAt != _grantedExpiresAt)
            {
                _grantedExpiresAt = state.SummonExpiresAt;
                PlayGranted();
            }
            else if (!isLive && _wasLive && !_hasSummoned)
            {
                ShowLine("소환 권리가 사라졌다", _lapsedColor);
            }

            _wasLive = isLive;

            if (!isLive)
            {
                HideRight();
                return;
            }

            bool isHurry = left <= _hurrySeconds;
            Color color = isHurry ? _hurryColor : _readyColor;
            float rate = isHurry ? _hurryPulseRate : _pulseRate;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * rate * 2f * Mathf.PI);
            // Downed keeps the clock running but the button cannot be pressed, so only the bar stays lively.
            bool canPress = state.CanSummon;

            _timer.SetActive(true);
            _timerFill.fillAmount = Mathf.Clamp01((float)(left / IdentityCheckService.SummonWindowSeconds));
            _timerFill.color = color;
            _glow.gameObject.SetActive(true);
            color.a = canPress ? Mathf.Lerp(0.35f, 0.9f, wave) : 0.2f;
            _glow.color = color;
            _summonButton.localScale = Vector3.one * (canPress ? 1f + _pulseScale * wave : 1f);

            int seconds = isHurry ? Mathf.CeilToInt((float)left) : -1;

            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _seconds.gameObject.SetActive(seconds > 0);
                _seconds.text = seconds > 0 ? seconds.ToString() : string.Empty;
                _seconds.color = _hurryColor;
            }
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.Result != IdentityActionResult.SummonApproved || !response.IsNewDecision)
            {
                return;
            }

            _hasSummoned = true;
            ShowLine("크툴루가 깨어났다 — 최후 전투 시작", _hurryColor);
        }

        private void PlayGranted()
        {
            _flashStartedAt = Time.unscaledTime;
            _vignette.gameObject.SetActive(true);
            ShowLine("바다가 응답했다 — 지금 크툴루를 소환할 수 있다", _readyColor);

            if (_audio && _grantedClip)
            {
                _audio.PlayOneShot(_grantedClip);
            }
        }

        private void ShowLine(string message, Color color)
        {
            _lineText.text = message;
            _lineText.color = color;
            _lineStartedAt = Time.unscaledTime;
            _line.alpha = 0f;
            _line.gameObject.SetActive(true);
        }

        private void RefreshFlash()
        {
            if (_flashStartedAt < 0f)
            {
                return;
            }

            float t = (Time.unscaledTime - _flashStartedAt) / FlashSeconds;

            if (t >= 1f)
            {
                _flashStartedAt = -1f;
                _vignette.gameObject.SetActive(false);
                return;
            }

            // Sharp rise, slow fall.
            Color color = _readyColor;
            color.a = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            _vignette.color = color;
        }

        private void RefreshLine()
        {
            if (_lineStartedAt < 0f)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _lineStartedAt;
            float total = _lineSeconds + LineFadeSeconds * 2f;

            if (elapsed >= total)
            {
                _lineStartedAt = -1f;
                _line.gameObject.SetActive(false);
                return;
            }

            _line.alpha = Mathf.Min(1f, Mathf.Min(elapsed, total - elapsed) / LineFadeSeconds);
        }

        private void HideRight()
        {
            if (_summonButton)
            {
                _summonButton.localScale = Vector3.one;
            }

            _glow.gameObject.SetActive(false);
            _timer.SetActive(false);
            _seconds.gameObject.SetActive(false);
            _shownSeconds = -1;
        }

        // Drawn here instead of shipped as pictures until the art arrives: a soft-edged box that slices around any
        // button size, and an edge-dark vignette for the whole screen.
        private void CreateSprites()
        {
            _glowTexture = NewTexture(GlowTextureSize);
            float half = GlowTextureSize * 0.5f;

            for (int y = 0; y < GlowTextureSize; y++)
            {
                for (int x = 0; x < GlowTextureSize; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, GlowTextureSize - 1 - x), Mathf.Min(y, GlowTextureSize - 1 - y));
                    float alpha = Mathf.SmoothStep(0f, 1f, edge / (half * 0.75f));
                    _glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _glowTexture.Apply(false, true);
            _glowSprite = Sprite.Create(_glowTexture, new Rect(0f, 0f, GlowTextureSize, GlowTextureSize),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(GlowBorder, GlowBorder, GlowBorder, GlowBorder));

            _vignetteTexture = NewTexture(VignetteTextureSize);
            float centre = (VignetteTextureSize - 1) * 0.5f;

            for (int y = 0; y < VignetteTextureSize; y++)
            {
                for (int x = 0; x < VignetteTextureSize; x++)
                {
                    float distance = new Vector2((x - centre) / centre, (y - centre) / centre).magnitude / Mathf.Sqrt(2f);
                    float alpha = Mathf.SmoothStep(0f, 0.85f, Mathf.InverseLerp(0.45f, 1f, distance));
                    _vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _vignetteTexture.Apply(false, true);
            _vignetteSprite = Sprite.Create(_vignetteTexture, new Rect(0f, 0f, VignetteTextureSize, VignetteTextureSize),
                new Vector2(0.5f, 0.5f));
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }
    }
}
