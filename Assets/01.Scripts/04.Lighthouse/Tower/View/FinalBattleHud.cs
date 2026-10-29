using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // Top-of-screen HUD for the final battle (2026-10-23 sketch): repair on the left, the lighthouse's health on the
    // right. Shown only while the phase is FinalBattle. The match values are SyncVars without hooks, so it reads them
    // every frame and rebuilds text only when a shown number changes. Bars ease toward the synced value; a hit flashes
    // the health bar.
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
        [SerializeField]
        private Color _healthHitColor = new Color(1f, 0.93f, 0.8f);
        [SerializeField, Min(0f)]
        private float _hitFlashSeconds = 0.25f;

        [Header("Motion")]
        [Tooltip("Bar fill change per second while easing toward the synced value.")]
        [SerializeField, Min(0.01f)]
        private float _fillSpeed = 1.2f;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private Color _healthColor;
        private float _flashTimer;
        private int _shownRepairTenths = -1;
        private int _shownHealth = -1;
        private int _shownMaxHealth = -1;
        private bool _isShown;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _healthColor = _healthFill ? _healthFill.color : Color.white;
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
                _flashTimer = _hitFlashSeconds;
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
