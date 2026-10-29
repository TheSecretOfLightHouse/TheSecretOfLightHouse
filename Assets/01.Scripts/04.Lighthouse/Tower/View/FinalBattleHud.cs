using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // Top-of-screen HUD for the final battle (2026-10-23 sketch): repair on the left, the lighthouse's health on the
    // right. Shown only while the phase is FinalBattle. The match values are SyncVars without hooks, so it reads them
    // every frame and rebuilds text only when a shown number changes. Bars ease toward the synced value. The strip
    // drops in from above when the battle starts; a hit shakes the health bar, flashes it red and floats the damage
    // (UI effects 2026-10-26, no camera shake since the camera is the Player part's).
    [DisallowMultipleComponent]
    public sealed class FinalBattleHud : MonoBehaviour
    {
        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private GameObject _root;

        [Header("Repair")]
        [SerializeField]
        private Image _repairFill;
        [SerializeField]
        private Text _repairText;

        [Header("Health")]
        [SerializeField]
        private Image _healthFill;
        [SerializeField]
        private Text _healthText;
        [Tooltip("Floats up next to the health on a hit. Hits close together add up.")]
        [SerializeField]
        private Text _damageText;
        [SerializeField]
        private Color _healthHitColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField, Min(0f)]
        private float _hitFlashSeconds = 0.35f;

        [Header("Motion")]
        [Tooltip("Bar fill change per second while easing toward the synced value.")]
        [SerializeField, Min(0.01f)]
        private float _fillSpeed = 1.2f;
        [SerializeField, Min(0f)]
        private float _dropDistance = 220f;
        [SerializeField, Min(0.01f)]
        private float _dropSeconds = 0.6f;
        [SerializeField, Min(0f)]
        private float _shakeDistance = 9f;
        [SerializeField, Min(0.01f)]
        private float _shakeSeconds = 0.35f;
        [SerializeField, Min(0.01f)]
        private float _damageSeconds = 1f;
        [SerializeField, Min(0f)]
        private float _damageRise = 30f;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private Color _healthColor;
        private float _flashTimer;
        private float _dropTimer;
        private float _shakeTimer;
        private float _damageTimer;
        private int _damageTotal;
        private int _shownRepairTenths = -1;
        private int _shownHealth = -1;
        private int _shownMaxHealth = -1;
        private bool _isShown;
        private RectTransform _hud;
        private Vector2 _hudPosition;
        private RectTransform _healthBar;
        private Vector2 _healthBarPosition;
        private RectTransform _damage;
        private Vector2 _damagePosition;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _healthColor = _healthFill ? _healthFill.color : Color.white;
            _hud = (RectTransform)_root.transform;
            _hudPosition = _hud.anchoredPosition;

            // The fill sits inside the bar's track and frame, so the bar itself is what shakes.
            if (_healthFill)
            {
                _healthBar = _healthFill.transform.parent as RectTransform;
                _healthBarPosition = _healthBar ? _healthBar.anchoredPosition : Vector2.zero;
            }

            if (_damageText)
            {
                _damage = _damageText.rectTransform;
                _damagePosition = _damage.anchoredPosition;
                _damageText.enabled = false;
            }

            _root.SetActive(false);
        }

        private void LateUpdate()
        {
            bool isFinalBattle = _match && _match.Phase == TowerPhase.FinalBattle;

            if (isFinalBattle != _isShown)
            {
                _isShown = isFinalBattle;
                _root.SetActive(isFinalBattle);

                // Opens on the current values instead of easing in from the last battle's.
                if (isFinalBattle)
                {
                    SnapBars();
                    _dropTimer = _dropSeconds;
                }
            }

            if (!_isShown)
            {
                return;
            }

            float repairRatio = RepairRatio();
            float healthRatio = HealthRatio();
            Ease(_repairFill, repairRatio);
            Ease(_healthFill, healthRatio);
            RefreshRepairText(repairRatio);
            RefreshHealthText();
            Flash();
            Drop();
            Shake();
            FloatDamage();
        }

        public void Bind(TowerMatchNet match)
        {
            _match = match;
            _isShown = false;
            _shownRepairTenths = -1;
            _shownHealth = -1;
            _shownMaxHealth = -1;
            _root.SetActive(false);
        }

        private float RepairRatio()
        {
            return _match.Tower ? Mathf.Clamp01(_match.Tower.RepairRatio) : 0f;
        }

        private float HealthRatio()
        {
            return _match.MaxHealth > 0f ? Mathf.Clamp01(_match.Health / _match.MaxHealth) : 0f;
        }

        private void SnapBars()
        {
            if (_repairFill)
            {
                _repairFill.fillAmount = RepairRatio();
            }

            if (_healthFill)
            {
                _healthFill.fillAmount = HealthRatio();
            }

            _shownHealth = -1;
            _flashTimer = 0f;
            _shakeTimer = 0f;
            _damageTimer = 0f;
            _damageTotal = 0;

            if (_damageText)
            {
                _damageText.enabled = false;
            }
        }

        private void Ease(Image fill, float target)
        {
            if (fill)
            {
                fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, target, _fillSpeed * Time.unscaledDeltaTime);
            }
        }

        private void RefreshRepairText(float ratio)
        {
            int tenths = Mathf.RoundToInt(ratio * 1000f);

            if (!_repairText || tenths == _shownRepairTenths)
            {
                return;
            }

            _shownRepairTenths = tenths;
            _repairText.text = $"{tenths * 0.1f:0.#}%";
        }

        private void RefreshHealthText()
        {
            int health = Mathf.CeilToInt(_match.Health);
            int maxHealth = Mathf.CeilToInt(_match.MaxHealth);

            if (health == _shownHealth && maxHealth == _shownMaxHealth)
            {
                return;
            }

            // A drop after the first value is a hit.
            if (_shownHealth >= 0 && health < _shownHealth)
            {
                Hit(_shownHealth - health);
            }

            _shownHealth = health;
            _shownMaxHealth = maxHealth;

            if (_healthText)
            {
                _healthText.text = $"{health} / {maxHealth}";
            }
        }

        private void Flash()
        {
            if (!_healthFill)
            {
                return;
            }

            _flashTimer = Mathf.Max(0f, _flashTimer - Time.unscaledDeltaTime);
            float amount = _hitFlashSeconds > 0f ? _flashTimer / _hitFlashSeconds : 0f;
            _healthFill.color = Color.Lerp(_healthColor, _healthHitColor, amount);
        }

        private void Hit(int damage)
        {
            _flashTimer = _hitFlashSeconds;
            _shakeTimer = _shakeSeconds;
            // Hits close together show as one growing number instead of overwriting each other.
            _damageTotal = _damageTimer > 0f ? _damageTotal + damage : damage;
            _damageTimer = _damageSeconds;

            if (_damageText)
            {
                _damageText.text = $"-{_damageTotal}";
                _damageText.enabled = true;
            }
        }

        // Eases out from above, so it lands softly.
        private void Drop()
        {
            if (_dropTimer <= 0f)
            {
                return;
            }

            _dropTimer = Mathf.Max(0f, _dropTimer - Time.unscaledDeltaTime);
            float left = _dropTimer / _dropSeconds;
            _hud.anchoredPosition = _hudPosition + new Vector2(0f, _dropDistance * left * left * left);
        }

        private void Shake()
        {
            if (!_healthBar || _shakeTimer <= 0f)
            {
                return;
            }

            _shakeTimer = Mathf.Max(0f, _shakeTimer - Time.unscaledDeltaTime);
            float left = _shakeTimer / _shakeSeconds;
            float offset = Mathf.Sin(_shakeTimer * 70f) * _shakeDistance * left;
            _healthBar.anchoredPosition = _healthBarPosition + new Vector2(offset, 0f);
        }

        private void FloatDamage()
        {
            if (!_damageText || _damageTimer <= 0f)
            {
                return;
            }

            _damageTimer = Mathf.Max(0f, _damageTimer - Time.unscaledDeltaTime);
            float done = 1f - _damageTimer / _damageSeconds;
            _damage.anchoredPosition = _damagePosition + new Vector2(0f, _damageRise * done);
            Color color = _damageText.color;
            color.a = 1f - done * done;
            _damageText.color = color;

            if (_damageTimer <= 0f)
            {
                _damageText.enabled = false;
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
