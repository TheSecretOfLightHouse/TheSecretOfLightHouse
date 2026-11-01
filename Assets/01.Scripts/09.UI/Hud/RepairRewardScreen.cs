using Lighthouse.Network.Session.View;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // The lighthouse repair reward window (2026-10-31): the current repair, then one row per reward percent of the
    // tower layout and a last row for 100%. A row is checked when the local player actually holds that reward, read
    // from the player's synced reward tier, so the tiers frozen at the summon and a summoned mafia's lost rewards show
    // as they apply. The 100% row is checked by the tower itself. Where HudReveal runs, the HUD button stays hidden and
    // its C key hint row opens the window (2026-10-31). Esc, the button again or a click outside closes it.
    [DisallowMultipleComponent]
    public sealed class RepairRewardScreen : MonoBehaviour
    {
        [Tooltip("Optional. Left empty, the active tower is looked up once a second, for scenes that spawn it at match start.")]
        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private GameObject _hud;
        [SerializeField]
        private Button _openButton;
        [SerializeField]
        private GameObject _window;
        [SerializeField]
        private Button _backdrop;
        [SerializeField]
        private TMP_Text _percentText;
        [Tooltip("{0} is the repair percent. The caption rides in the same text so it follows the number's width.")]
        [SerializeField]
        private string _percentFormat = "{0}%<size=36%><color=#FAEBC9>  현재 수리도</color></size>";
        [Tooltip("The full bar; the fill is anchored to its left edge and widens with the repair.")]
        [SerializeField]
        private RectTransform _barTrack;
        [SerializeField]
        private RectTransform _barFill;
        [Tooltip("Rows top to bottom: one per reward percent of the tower layout, then the 100% row.")]
        [SerializeField]
        private RewardRow[] _rows = new RewardRow[0];
        [SerializeField]
        private string _ritualName = "점화 의식 가능";
        [Tooltip("The 100% row in the final battle, where finishing the repair is the citizens' win.")]
        [SerializeField]
        private string _finalBattleGoal = "수리 완료 시 시민 승리";
        [SerializeField]
        private TMP_Text _footer;
        [SerializeField]
        private string _footerText = "보상은 자동으로 적용됩니다";
        [SerializeField]
        private string _finalBattleFooterText = "최후 전투 중에는 새 보상이 열리지 않습니다";
        [SerializeField]
        private Color _reachedColor = new Color(0.98f, 0.92f, 0.79f);
        [SerializeField]
        private Color _dimColor = new Color(0.6f, 0.55f, 0.48f);
        [SerializeField]
        private Sprite _checkSprite;
        [SerializeField]
        private Sprite _openSprite;

        [System.Serializable]
        private struct RewardRow
        {
            public TMP_Text Percent;
            public TMP_Text Label;
            public Image Mark;
        }

        private const float MatchLookupInterval = 1f;

        private bool _isOpen;
        private float _nextMatchLookup;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        // Last drawn values, so the texts are rebuilt only when something changes.
        private int _shownPercent = -1;
        private int _shownTier = -1;
        private bool _shownComplete;
        private bool _shownFinalBattle;

        private void Awake()
        {
            _openButton.onClick.AddListener(Toggle);

            if (_backdrop)
            {
                _backdrop.onClick.AddListener(Close);
            }

            _window.SetActive(false);
            _hud.SetActive(false);
        }

        private void OnDisable()
        {
            Close();
        }

        private void Update()
        {
            FindMatch();
            TowerNet tower = _match && _match.netId != 0 ? _match.Tower : null;
            bool isPlaying = tower && tower.Layout && NetworkClient.localPlayer && _match.Phase != TowerPhase.Ended;

            if (_hud.activeSelf != isPlaying)
            {
                _hud.SetActive(isPlaying);
            }

            if (!isPlaying)
            {
                Close();
                return;
            }

            if (!_isOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            Refresh(tower);
        }

        // The main island spawns its tower at match start and keeps a disabled legacy one in the scene (2026-10-30), so
        // only an active tower counts, looked up once a second until one appears.
        private void FindMatch()
        {
            if (_match && !_match.gameObject.activeInHierarchy)
            {
                _match = null;
            }

            if (_match || Time.unscaledTime < _nextMatchLookup)
            {
                return;
            }

            _nextMatchLookup = Time.unscaledTime + MatchLookupInterval;
            _match = FindAnyObjectByType<TowerMatchNet>();
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (_isOpen || !_hud.activeSelf || SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }

            _isOpen = true;
            _shownPercent = -1;
            _window.SetActive(true);
            Refresh(_match.Tower);
            ShowCursor(true);
            SessionScreen.SetGameplayOverlayOpen(this, true);
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _window.SetActive(false);
            ShowCursor(false);
            SessionScreen.SetGameplayOverlayOpen(this, false);
        }

        private void Refresh(TowerNet tower)
        {
            int percent = Mathf.RoundToInt(tower.RepairRatio * 100f);
            int tier = GetOwnTier(tower);
            bool isComplete = tower.IsComplete;
            bool isFinalBattle = _match.Phase == TowerPhase.FinalBattle;

            if (percent == _shownPercent && tier == _shownTier && isComplete == _shownComplete && isFinalBattle == _shownFinalBattle)
            {
                return;
            }

            _shownPercent = percent;
            _shownTier = tier;
            _shownComplete = isComplete;
            _shownFinalBattle = isFinalBattle;

            _percentText.text = string.Format(_percentFormat, percent);
            float width = _barTrack.rect.width;
            float minWidth = _barFill.rect.height;
            _barFill.gameObject.SetActive(percent > 0);
            _barFill.sizeDelta = new Vector2(Mathf.Max(minWidth, width * tower.RepairRatio), _barFill.sizeDelta.y);

            TowerLayout layout = tower.Layout;
            int rewardCount = Mathf.Min(layout.RewardTierCount, _rows.Length - 1);

            for (int i = 0; i < _rows.Length; i++)
            {
                bool isRitualRow = i == _rows.Length - 1;
                bool isUsed = isRitualRow || i < rewardCount;
                _rows[i].Percent.gameObject.SetActive(isUsed);
                _rows[i].Label.gameObject.SetActive(isUsed);
                _rows[i].Mark.gameObject.SetActive(isUsed);

                if (!isUsed)
                {
                    continue;
                }

                if (isRitualRow)
                {
                    SetRow(_rows[i], 100, isFinalBattle ? _finalBattleGoal : _ritualName, isComplete);
                }
                else
                {
                    SetRow(_rows[i], layout.GetRewardPercent(i), layout.GetRewardName(i), i < tier);
                }
            }

            if (_footer)
            {
                _footer.text = isFinalBattle ? _finalBattleFooterText : _footerText;
            }
        }

        private void SetRow(RewardRow row, int percent, string label, bool isReached)
        {
            Color color = isReached ? _reachedColor : _dimColor;
            row.Percent.text = $"{percent}%";
            row.Percent.color = color;
            row.Label.text = label;
            row.Label.color = color;
            row.Mark.sprite = isReached ? _checkSprite : _openSprite;
        }

        // The tier the local player holds (synced, frozen at the summon, none for a summoned mafia); the tower's tier
        // stands in only when the player has no reward component.
        private static int GetOwnTier(TowerNet tower)
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            if (player && player.TryGetComponent(out PlayerPassiveController passive))
            {
                return passive.IsJobAbilityEnabled ? passive.TowerRewardTier : 0;
            }

            return tower.RewardTier;
        }

        // Gameplay hides the cursor; a click outside the board needs it while the window is open.
        private void ShowCursor(bool isShown)
        {
            if (isShown)
            {
                if (!_hasSavedCursor)
                {
                    _hasSavedCursor = true;
                    _savedLockMode = Cursor.lockState;
                    _savedCursorVisible = Cursor.visible;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (_hasSavedCursor)
            {
                _hasSavedCursor = false;
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }
        }
    }
}
