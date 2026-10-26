using Lighthouse.Common.UI;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // Screen-only effects for the lighthouse match (UI effects plan 2026-10-26): lines on the shared ScreenNotice strip
    // for installs, finished floors, the last part and finished modules; a centre title for full repair, ritual,
    // summon, falling health and the end; the ritual countdown; and an edge flash standing in for camera shake (the
    // camera is the Player part's). Who installed or crafted is never shown, since only the carrier knows they held a
    // module.
    // Installs and finished modules arrive as events on every machine. Phase and health are SyncVars without hooks and
    // several of them change together (phase first), so they are read here every frame; by LateUpdate the whole change
    // has landed on host and client alike. The first phase seen is taken as it is, so a late joiner gets the steady
    // state without replays.
    [DisallowMultipleComponent]
    public sealed class TowerMatchEffects : MonoBehaviour
    {
        [SerializeField]
        private TowerMatchNet _match;

        [Header("Title")]
        [SerializeField]
        private CanvasGroup _title;
        [SerializeField]
        private Text _titleText;
        [SerializeField, Min(0.1f)]
        private float _titleSeconds = 2.8f;

        [Header("Ritual")]
        [SerializeField]
        private Text _countdownText;
        [SerializeField]
        private Color _countdownColor = new Color(0.96f, 0.89f, 0.74f);
        [SerializeField]
        private Color _countdownWarningColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField, Min(0)]
        private int _countdownWarningSeconds = 10;

        [Header("Edge")]
        [Tooltip("Full-screen image whose sprite is built here: clear in the middle, solid at the edges.")]
        [SerializeField]
        private Image _edge;
        [SerializeField]
        private Color _readyColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField]
        private Color _summonColor = new Color(0.85f, 0.08f, 0.08f);
        [Tooltip("Below this share of health the edge keeps pulsing red during the final battle, and the first drop below it gets a title.")]
        [SerializeField, Range(0f, 1f)]
        private float _lowHealthRatio = 0.3f;

        [Header("End")]
        [SerializeField]
        private Image _fade;
        [SerializeField, Range(0f, 1f)]
        private float _fadeAlpha = 0.72f;
        [SerializeField, Min(0.1f)]
        private float _fadeSeconds = 2.5f;

        [Header("Sound")]
        [Tooltip("Optional. Empty clips are skipped; the project has no game sounds yet (2026-10-26).")]
        [SerializeField]
        private AudioSource _audio;
        [SerializeField]
        private AudioClip _installClip;
        [SerializeField]
        private AudioClip _floorClip;
        [SerializeField]
        private AudioClip _readyClip;
        [SerializeField]
        private AudioClip _ritualClip;
        [SerializeField]
        private AudioClip _summonClip;
        [SerializeField]
        private AudioClip _hitClip;
        [SerializeField]
        private AudioClip _endClip;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private float _titleTimer;
        private bool _isTitleHeld;

        private float _edgeTimer;
        private float _edgeSeconds;
        private float _edgePeak;
        private Color _edgeColor;

        private float _fadeTimer;
        private int _shownCountdown = -1;
        private bool _hasPhase;
        private TowerPhase _shownPhase;
        private float _shownHealth = -1f;
        private bool _hasWarnedLowHealth;
        private bool _isReadyPending;
        private TowerNet _tower;
        private WorkbenchNet _workbench;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            if (_edge)
            {
                _edge.sprite = BuildEdgeSprite();
            }

            SetAlpha(_title, 0f);
            SetImageAlpha(_edge, 0f);
            SetImageAlpha(_fade, 0f);

            if (_countdownText)
            {
                _countdownText.enabled = false;
            }
        }

        private void OnEnable()
        {
            _tower = _match ? _match.Tower : null;

            // Awake of the match may run after this one, so fall back to the component beside it.
            if (!_tower && _match)
            {
                _tower = _match.GetComponent<TowerNet>();
            }

            _workbench = _tower ? _tower.Workbench : null;

            if (_tower)
            {
                _tower.SlotInstalled += HandleSlotInstalled;
            }

            if (_workbench)
            {
                _workbench.RecipeCompleted += HandleRecipeCompleted;
            }
        }

        private void LateUpdate()
        {
            if (!_match)
            {
                return;
            }

            ObservePhase();
            ObserveHealth();
            UpdateTitle();
            UpdateCountdown();
            UpdateEdge();
            UpdateFade();
        }

        private void OnDisable()
        {
            if (_tower)
            {
                _tower.SlotInstalled -= HandleSlotInstalled;
            }

            if (_workbench)
            {
                _workbench.RecipeCompleted -= HandleRecipeCompleted;
            }
        }

        private void HandleSlotInstalled(int slot)
        {
            TowerLayout layout = _tower.Layout;

            if (!layout || slot < 0 || slot >= layout.SlotCount)
            {
                return;
            }

            int count = _tower.InstalledCount;
            int total = layout.SlotCount;
            float before = layout.GetRepairRatio(count - 1);
            float after = layout.GetRepairRatio(count);
            ScreenNotice.Show(new ScreenNoticeRequest($"등대 부품 장착 ({count}/{total})")
                .WithGauge(before, after, $"+{(after - before) * 100f:0.#}%"));
            Play(_installClip);

            int floor = layout[slot].Floor;

            // The top floor finishing is the full repair, which gets the centre title instead.
            if (count < total && _tower.IsFloorComplete(floor))
            {
                bool isReward = layout.GetRewardTier(count) > layout.GetRewardTier(count - 1);
                string text = isReward ? $"{layout.GetFloorName(floor)} 수리 완료 — 보상 해금" : $"{layout.GetFloorName(floor)} 수리 완료";
                ScreenNotice.Show(new ScreenNoticeRequest(text, ScreenNoticeStyle.Gold).WithGauge(after, after));
                Play(_floorClip);
            }

            if (count == total - 1)
            {
                ScreenNotice.Show(new ScreenNoticeRequest("마지막 부품 — 1칸 남음", ScreenNoticeStyle.Gold).WithGauge(after, after));
            }

            // Decided in LateUpdate: a remote client may see the slot before the phase that goes with it.
            if (count == total)
            {
                _isReadyPending = true;
            }
        }

        private void HandleRecipeCompleted(int recipeId)
        {
            if (_workbench.Catalog && _workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe))
            {
                ScreenNotice.Show(new ScreenNoticeRequest($"{recipe.DisplayName} 완성", ScreenNoticeStyle.Normal, recipe.Icon));
            }
        }

        private void ObservePhase()
        {
            TowerPhase phase = _match.Phase;

            if (!_hasPhase)
            {
                _hasPhase = true;
                _shownPhase = phase;
                _isReadyPending = false;

                if (phase == TowerPhase.Ended)
                {
                    ShowEnd(false);
                }

                return;
            }

            if (_isReadyPending)
            {
                _isReadyPending = false;

                if (phase == TowerPhase.Repairing && _tower && _tower.IsComplete)
                {
                    ShowTitle("등대를 밝힐 준비가 끝났다", false);
                    FlashEdge(_readyColor, 0.55f, 1.6f);
                    Play(_readyClip);
                }
            }

            if (phase == _shownPhase)
            {
                return;
            }

            _shownPhase = phase;

            switch (phase)
            {
                case TowerPhase.Ritual:
                    ShowTitle("점화 의식 시작", false);
                    FlashEdge(_readyColor, 0.4f, 1.2f);
                    Play(_ritualClip);
                    break;
                case TowerPhase.FinalBattle:
                    ShowTitle("심해의 괴물이 깨어났다", false);
                    FlashEdge(_summonColor, 0.9f, 2f);
                    Play(_summonClip);
                    break;
                case TowerPhase.Ended:
                    ShowEnd(true);
                    break;
            }
        }

        private void ObserveHealth()
        {
            float health = _match.Health;
            bool isBattle = _shownPhase == TowerPhase.FinalBattle;

            // Only a drop inside the battle is a hit; the battle's start fills the health up.
            if (isBattle && _shownHealth >= 0f && health < _shownHealth)
            {
                Play(_hitClip);

                // Once per match, when the lighthouse first falls into the red.
                float max = _match.MaxHealth;

                if (!_hasWarnedLowHealth && max > 0f && health > 0f && health / max < _lowHealthRatio
                    && _shownHealth / max >= _lowHealthRatio)
                {
                    _hasWarnedLowHealth = true;
                    ShowTitle("등대가 무너지고 있다", false);
                    FlashEdge(_summonColor, 0.6f, 1.4f);
                }
            }

            _shownHealth = health;
        }

        private void ShowEnd(bool isAnimated)
        {
            ScreenNotice.Clear();
            string text = _match.Outcome == TowerOutcome.CitizensWin ? "등대에 불이 켜졌다 — 탈출하라"
                : _match.Outcome == TowerOutcome.MafiaWin ? "등대는 어둠에 잠겼다" : string.Empty;
            ShowTitle(text, true);
            _fadeTimer = isAnimated ? _fadeSeconds : 0f;

            if (!isAnimated)
            {
                SetAlpha(_title, 1f);
                SetImageAlpha(_fade, _fadeAlpha);
            }

            Play(_endClip);
        }

        private void ShowTitle(string text, bool isHeld)
        {
            if (!_title || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_titleText)
            {
                _titleText.text = text;
            }

            _isTitleHeld = isHeld;
            _titleTimer = _titleSeconds;
        }

        private void UpdateTitle()
        {
            if (!_title || _titleTimer <= 0f)
            {
                return;
            }

            _titleTimer = Mathf.Max(0f, _titleTimer - Time.unscaledDeltaTime);
            float elapsed = _titleSeconds - _titleTimer;
            float fadeIn = Mathf.Clamp01(elapsed / 0.35f);
            float fadeOut = _isTitleHeld ? 1f : Mathf.Clamp01(_titleTimer / 0.6f);
            _title.alpha = Mathf.Min(fadeIn, fadeOut);
            // Lands from slightly larger.
            float scale = 1f + 0.15f * (1f - fadeIn) * (1f - fadeIn);
            _title.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateCountdown()
        {
            if (!_countdownText)
            {
                return;
            }

            bool isRitual = _shownPhase == TowerPhase.Ritual;

            if (_countdownText.enabled != isRitual)
            {
                _countdownText.enabled = isRitual;
                _shownCountdown = -1;
            }

            if (!isRitual)
            {
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(_match.RitualEndsAt - NetworkTime.time)));

            if (seconds != _shownCountdown)
            {
                _shownCountdown = seconds;
                _countdownText.text = $"점화 의식 {seconds}초";
            }

            // The last seconds beat once a second, red.
            bool isWarning = seconds <= _countdownWarningSeconds;
            float beat = isWarning ? 1f - Mathf.Repeat((float)(_match.RitualEndsAt - NetworkTime.time), 1f) : 0f;
            float pulse = isWarning ? Mathf.Clamp01(1f - beat * 2.5f) : 0f;
            _countdownText.color = isWarning ? Color.Lerp(_countdownWarningColor, Color.white, pulse * 0.4f) : _countdownColor;
            float scale = 1f + 0.18f * pulse;
            _countdownText.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void FlashEdge(Color color, float peak, float seconds)
        {
            _edgeColor = color;
            _edgePeak = peak;
            _edgeSeconds = seconds;
            _edgeTimer = seconds;
        }

        private void UpdateEdge()
        {
            if (!_edge)
            {
                return;
            }

            float flash = 0f;

            if (_edgeTimer > 0f)
            {
                _edgeTimer = Mathf.Max(0f, _edgeTimer - Time.unscaledDeltaTime);
                float left = _edgeTimer / _edgeSeconds;
                flash = _edgePeak * left * left;
            }

            float pulse = 0f;
            bool isLow = _shownPhase == TowerPhase.FinalBattle && _match.MaxHealth > 0f
                && _match.Health / _match.MaxHealth < _lowHealthRatio;

            if (isLow)
            {
                pulse = 0.12f + 0.16f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            }

            Color color = flash >= pulse ? _edgeColor : _summonColor;
            color.a = Mathf.Max(flash, pulse);
            _edge.color = color;
        }

        private void UpdateFade()
        {
            if (!_fade || _fadeTimer <= 0f)
            {
                return;
            }

            _fadeTimer = Mathf.Max(0f, _fadeTimer - Time.unscaledDeltaTime);
            SetImageAlpha(_fade, _fadeAlpha * (1f - _fadeTimer / _fadeSeconds));
        }

        private void Play(AudioClip clip)
        {
            if (_audio && clip)
            {
                _audio.PlayOneShot(clip);
            }
        }

        // Built once so the prefab needs no image asset: alpha rises from the middle toward the edges.
        private static Sprite BuildEdgeSprite()
        {
            const int Size = 64;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float distance = Mathf.Clamp01((Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.55f) / 0.45f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(distance * distance * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group)
            {
                group.alpha = alpha;
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image)
            {
                Color color = image.color;
                color.a = alpha;
                image.color = color;
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
