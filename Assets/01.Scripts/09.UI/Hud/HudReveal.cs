using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.View;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.UI.Hud
{
    // Folds the right-side buttons (identity check, summon, vote) and the top-right key hints away until Alt is pressed
    // (2026-10-31): folded, only the Esc and Alt rows stay. Alt toggles; it is ignored while a gameplay screen is open.
    // The repair reward is a key hint row (C) above the boat-only Q row; its own screen's button stays hidden. While the
    // final battle HUD shows, the key hints move down below it.
    // The panel also opens by itself while the summon right is live, and for a moment when the votes lock, a count left
    // changes, or the local player first appears. The V, Z, X and C keys work only while the panel shows. The own role mark
    // in the role row (mask for the mafia, lantern for a citizen) shows with the panel too. Everything is added
    // at runtime so the shared HUD prefabs stay as they are; a scene without this component keeps the always-on HUD.
    [DisallowMultipleComponent]
    public sealed class HudReveal : MonoBehaviour
    {
        private const string FoldedCaption = "안내";
        private const string OpenCaption = "안내 닫기";

        private static HudReveal _current;

        [Tooltip("Found in the scene when empty. Mirror keeps scene network objects inactive until they spawn.")]
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private Key _toggleKey = Key.LeftAlt;
        [SerializeField]
        private Key _checkKey = Key.Z;
        [SerializeField]
        private Key _summonKey = Key.X;
        [SerializeField]
        private Key _rewardKey = Key.C;
        [SerializeField, Min(0.01f)]
        private float _fadeSeconds = 0.15f;
        [Tooltip("How long the panel opens by itself when the votes lock or a count left changes.")]
        [SerializeField]
        private float _peekSeconds = 2f;
        [Tooltip("How long the panel opens by itself when the local player first appears.")]
        [SerializeField]
        private float _firstPeekSeconds = 3f;

        [Header("Key hints")]
        [Tooltip("The Esc row's label; the Alt row's label is copied from it.")]
        [SerializeField]
        private TMP_Text _exitLabel;
        [Tooltip("The row the Alt row takes the place of; it and the rows below move one row down.")]
        [SerializeField]
        private RectTransform _secondRowKey;
        [Tooltip("The wide key the Alt key copies its size and column from.")]
        [SerializeField]
        private RectTransform _wideKey;
        [SerializeField]
        private Sprite _altKeySprite;
        [Tooltip("The Esc key; the key hints move down together with it and its label.")]
        [SerializeField]
        private RectTransform _exitKey;
        [Tooltip("Key and label objects shown only while the panel is open.")]
        [SerializeField]
        private RectTransform[] _foldedHints = new RectTransform[0];
        [Tooltip("Folded rows that stay last (the boat-only Q row); the repair reward row goes above them.")]
        [SerializeField]
        private RectTransform[] _lastHints = new RectTransform[0];
        [Tooltip("A one-letter key the reward key copies its size and column from.")]
        [SerializeField]
        private RectTransform _narrowKey;
        [SerializeField]
        private Sprite _rewardKeySprite;
        [SerializeField]
        private string _rewardCaption = "수리 보상";

        [Header("Right side")]
        [SerializeField]
        private GameObject _voteHud;
        [Tooltip("The vote button's V key; the Z and X keys are copied from it.")]
        [SerializeField]
        private GameObject _voteKeyCap;
        [SerializeField]
        private GameObject _identityHud;
        [SerializeField]
        private Button _checkButton;
        [SerializeField]
        private Button _summonButton;
        [Tooltip("The repair reward screen's button group. It stays hidden; the C row shows while it is active.")]
        [SerializeField]
        private GameObject _rewardHud;
        [SerializeField]
        private Button _rewardButton;

        [Header("Final battle")]
        [Tooltip("Final battle HUD parts at the top right; while any shows, the key hints move below them.")]
        [SerializeField]
        private RectTransform[] _avoidRects = new RectTransform[0];
        [SerializeField]
        private float _avoidGap = 16f;
        [Tooltip("Key hint move speed in canvas units per second.")]
        [SerializeField, Min(1f)]
        private float _avoidSpeed = 900f;

        [Header("Own role")]
        [Tooltip("The job name in the role row. The own role mark is built at the right end of that row and shows only while"
            + " the panel is open (2026-10-31), so a glance at the screen does not give the mafia away.")]
        [SerializeField]
        private TMP_Text _roleName;
        [Tooltip("The panel behind the role row; the mark keeps this far in from its right edge.")]
        [SerializeField]
        private RectTransform _rolePanel;
        [SerializeField]
        private float _roleMarkInset = 28f;
        [Tooltip("The workbench withdraw button's mask.")]
        [SerializeField]
        private Sprite _mafiaMark;
        [Tooltip("The mask is drawn tilted; this turns it level (degrees, counter-clockwise).")]
        [SerializeField]
        private float _mafiaMarkTilt = -19f;
        [Tooltip("Citizens get a mark of the same size, so an open panel does not tell the roles apart by a missing mark.")]
        [SerializeField]
        private Sprite _citizenMark;
        [SerializeField]
        private Color _mafiaColor = new Color(1f, 0.42f, 0.36f);

        private CanvasGroup _hintGroup;
        private CanvasGroup _voteGroup;
        private CanvasGroup _identityGroup;
        private CanvasGroup _rewardGroup;
        private CanvasGroup _roleGroup;
        private Image _roleMarkIcon;
        private TMP_Text _roleMarkLabel;
        private MatchRole _shownRole;
        private TMP_Text _altLabel;
        private RectTransform _altKey;
        // One key hint row's height, taken from the Esc and second rows before anything moves.
        private float _rowStep;
        private GameObject _rewardRow;
        // The key hint pieces that move down together, with their resting y.
        private RectTransform[] _hintPieces = new RectTransform[0];
        private float[] _hintRestY = new float[0];
        private Canvas _hintCanvas;
        private float _hintDrop;
        private readonly Vector3[] _corners = new Vector3[4];
        private bool _isOpen;
        private bool _isShown;
        private bool _isPinned;
        private float _peekEndsAt;
        private float _alpha;
        private NetworkIdentity _player;
        // Last seen values, -1 until first seen, so the first value does not count as a change.
        private int _seenVotes = -1;
        private int _seenChecks = -1;
        private bool _wasLocked;

        // Shortcut keys elsewhere (the vote screen's V) ask this; a scene without the component keeps them always on.
        public static bool AllowsShortcuts => !_current || _current._isShown;

        private void Awake()
        {
            _current = this;
        }

        private void Start()
        {
            BuildAltRow();
            _hintGroup = FoldHints();
            BuildRewardRow();
            CollectHintPieces();
            _voteGroup = GetGroup(_voteHud);
            _identityGroup = GetGroup(_identityHud);
            // The reward screen's own button never shows; the C row stands for it.
            _rewardGroup = GetGroup(_rewardHud);
            SetInteractable(_rewardGroup, false);

            if (_rewardGroup)
            {
                _rewardGroup.alpha = 0f;
            }

            AddKeyCap(_checkButton, "Z");
            AddKeyCap(_summonButton, "X");
            BuildRoleMark();
            _isShown = true;
            SetShown(false);
            _alpha = 0f;
            ApplyAlpha();
        }

        private void OnEnable()
        {
            if (!_voting)
            {
                _voting = FindAnyObjectByType<VotingNetworkController>(FindObjectsInactive.Include);
            }
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Update()
        {
            TrackPlayer();
            TrackState();
            TrackRole();
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame && !SessionScreen.IsGameplayOverlayOpen)
            {
                _isOpen = !_isShown;
                _peekEndsAt = 0f;
            }

            SetShown(_isOpen || _isPinned || Time.unscaledTime < _peekEndsAt);
            _alpha = Mathf.MoveTowards(_alpha, _isShown ? 1f : 0f, Time.unscaledDeltaTime / _fadeSeconds);
            ApplyAlpha();
            bool hasReward = _rewardHud && _rewardHud.activeSelf;

            if (_rewardRow && _rewardRow.activeSelf != hasReward)
            {
                _rewardRow.SetActive(hasReward);
            }

            if (keyboard == null || !_isShown || SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }

            if (keyboard[_checkKey].wasPressedThisFrame)
            {
                Press(_checkButton);
            }
            else if (keyboard[_summonKey].wasPressedThisFrame)
            {
                Press(_summonButton);
            }
            else if (keyboard[_rewardKey].wasPressedThisFrame)
            {
                Press(_rewardButton);
            }
        }

        private void TrackPlayer()
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            if (player == _player)
            {
                return;
            }

            _player = player;
            _isOpen = false;
            _seenVotes = -1;
            _seenChecks = -1;
            _wasLocked = false;

            if (player)
            {
                Peek(_firstPeekSeconds);
            }
        }

        private void TrackState()
        {
            _isPinned = false;

            if (!_voting)
            {
                return;
            }

            if (_voting.HasOwnVoteState)
            {
                VoteResponse vote = _voting.OwnVoteState;

                if ((_seenVotes >= 0 && vote.RemainingVoteCount != _seenVotes) || (vote.IsLocked && !_wasLocked))
                {
                    Peek(_peekSeconds);
                }

                _seenVotes = vote.RemainingVoteCount;
                _wasLocked = vote.IsLocked;
            }

            if (_voting.HasOwnIdentityState)
            {
                IdentityResponse state = _voting.OwnIdentityState;

                if (_seenChecks >= 0 && state.RemainingCheckCount != _seenChecks)
                {
                    Peek(_peekSeconds);
                }

                _seenChecks = state.RemainingCheckCount;
                // The 10 second summon right must not be spent finding the button.
                _isPinned = state.CanSummon && state.HasSummonRight && NetworkTime.time < state.SummonExpiresAt;
            }
        }

        // The role arrives with the match start and goes back to None in the lobby; the mark follows it.
        private void TrackRole()
        {
            if (!_roleGroup)
            {
                return;
            }

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            MatchRole role = manager ? manager.OwnRole : MatchRole.None;

            if (role == _shownRole)
            {
                return;
            }

            _shownRole = role;
            bool isMafia = role == MatchRole.Mafia;
            _roleGroup.gameObject.SetActive(role != MatchRole.None);
            _roleMarkIcon.sprite = isMafia ? _mafiaMark : _citizenMark;
            _roleMarkIcon.enabled = _roleMarkIcon.sprite;
            _roleMarkIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, isMafia ? _mafiaMarkTilt : 0f);
            _roleMarkLabel.text = isMafia ? "마피아" : "선원";
            _roleMarkLabel.color = isMafia ? _mafiaColor : _roleName.color;
        }

        private void Peek(float seconds)
        {
            _peekEndsAt = Mathf.Max(_peekEndsAt, Time.unscaledTime + seconds);
        }

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown)
            {
                return;
            }

            _isShown = isShown;

            if (_altLabel)
            {
                _altLabel.text = isShown ? OpenCaption : FoldedCaption;
            }

            SetInteractable(_hintGroup, isShown);
            SetInteractable(_voteGroup, isShown);
            SetInteractable(_identityGroup, isShown);
        }

        private void ApplyAlpha()
        {
            if (_hintGroup)
            {
                _hintGroup.alpha = _alpha;
            }

            if (_voteGroup)
            {
                _voteGroup.alpha = _alpha;
            }

            if (_identityGroup)
            {
                _identityGroup.alpha = _alpha;
            }

            if (_roleGroup)
            {
                _roleGroup.alpha = _alpha;
            }
        }

        // The final battle HUD moves in its own Update (drop, shake), so its place is read after it.
        private void LateUpdate()
        {
            if (_hintPieces.Length == 0 || !_hintCanvas || _hintCanvas.scaleFactor <= 0f)
            {
                return;
            }

            float drop = Mathf.MoveTowards(_hintDrop, GetAvoidDrop(), _avoidSpeed * Time.unscaledDeltaTime);

            if (Mathf.Approximately(drop, _hintDrop))
            {
                return;
            }

            _hintDrop = drop;

            for (int i = 0; i < _hintPieces.Length; i++)
            {
                Vector2 position = _hintPieces[i].anchoredPosition;
                position.y = _hintRestY[i] - drop;
                _hintPieces[i].anchoredPosition = position;
            }
        }

        // How far below their resting place the key hints must sit to clear the shown final battle parts, in canvas
        // units. Both are screen space overlay canvases, so world corners are screen pixels.
        private float GetAvoidDrop()
        {
            float bottom = float.MaxValue;
            float right = float.MinValue;

            for (int i = 0; i < _avoidRects.Length; i++)
            {
                RectTransform rect = _avoidRects[i];

                if (!rect || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rect.GetWorldCorners(_corners);
                bottom = Mathf.Min(bottom, _corners[0].y);
                right = Mathf.Max(right, _corners[2].x);
            }

            if (bottom == float.MaxValue)
            {
                return 0f;
            }

            _exitKey.GetWorldCorners(_corners);

            // On a wide screen the parts can end left of the hints; then nothing has to move.
            if (right < _corners[0].x)
            {
                return 0f;
            }

            float scale = _hintCanvas.scaleFactor;
            float restTop = _corners[1].y + _hintDrop * scale;
            return Mathf.Max(0f, (restTop - bottom) / scale + _avoidGap);
        }

        // The owning screens decide when a button can be used; this only stands in for the click.
        private static void Press(Button button)
        {
            if (button && button.isActiveAndEnabled && button.interactable)
            {
                button.onClick.Invoke();
            }
        }

        // The Alt row is built beside the Esc row rather than copied from it: the Esc key carries the leave button.
        private void BuildAltRow()
        {
            if (!_exitLabel || !_secondRowKey || !_wideKey)
            {
                return;
            }

            float rowY = _secondRowKey.anchoredPosition.y;
            GameObject key = new GameObject("AltKey", typeof(RectTransform), typeof(Image));
            RectTransform keyRect = (RectTransform)key.transform;
            keyRect.SetParent(_wideKey.parent, false);
            keyRect.anchorMin = _wideKey.anchorMin;
            keyRect.anchorMax = _wideKey.anchorMax;
            keyRect.pivot = _wideKey.pivot;
            keyRect.sizeDelta = _wideKey.sizeDelta;
            keyRect.anchoredPosition = new Vector2(_wideKey.anchoredPosition.x, rowY);
            Image image = key.GetComponent<Image>();
            image.sprite = _altKeySprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            _altKey = keyRect;

            _altLabel = Instantiate(_exitLabel, _exitLabel.transform.parent);
            _altLabel.name = "AltLabel";
            _altLabel.text = FoldedCaption;
            RectTransform labelRect = _altLabel.rectTransform;
            labelRect.anchoredPosition = new Vector2(labelRect.anchoredPosition.x, rowY);
        }

        // The reward row takes the place of the last rows (Q), which move one row further down. It sits in the folded group,
        // so it fades with the panel, and shows only while the reward screen's own button would.
        private void BuildRewardRow()
        {
            if (!_hintGroup || !_rewardHud || !_narrowKey || _lastHints.Length == 0)
            {
                return;
            }

            RectTransform group = (RectTransform)_hintGroup.transform;
            float rowY = _lastHints[0].anchoredPosition.y;

            for (int i = 0; i < _lastHints.Length; i++)
            {
                _lastHints[i].anchoredPosition -= new Vector2(0f, _rowStep);
            }

            _rewardRow = new GameObject("RewardRow", typeof(RectTransform));
            RectTransform rowRect = (RectTransform)_rewardRow.transform;
            rowRect.SetParent(group, false);
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.pivot = group.pivot;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            GameObject key = new GameObject("RewardKey", typeof(RectTransform), typeof(Image));
            RectTransform keyRect = (RectTransform)key.transform;
            keyRect.SetParent(rowRect, false);
            keyRect.anchorMin = _narrowKey.anchorMin;
            keyRect.anchorMax = _narrowKey.anchorMax;
            keyRect.pivot = _narrowKey.pivot;
            keyRect.sizeDelta = _narrowKey.sizeDelta;
            keyRect.anchoredPosition = new Vector2(_narrowKey.anchoredPosition.x, rowY);
            Image image = key.GetComponent<Image>();
            image.sprite = _rewardKeySprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            TMP_Text label = Instantiate(_exitLabel, rowRect);
            label.name = "RewardLabel";
            label.text = _rewardCaption;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchoredPosition = new Vector2(labelRect.anchoredPosition.x, rowY);
            _rewardRow.SetActive(false);
        }

        // Every key hint piece moves down as one block; the folded and reward rows ride in their group.
        private void CollectHintPieces()
        {
            if (!_exitKey || !_exitLabel)
            {
                return;
            }

            _hintCanvas = _exitKey.GetComponentInParent<Canvas>(true).rootCanvas;
            RectTransform[] pieces =
            {
                _exitKey,
                _exitLabel.rectTransform,
                _altKey,
                _altLabel ? _altLabel.rectTransform : null,
                _hintGroup ? (RectTransform)_hintGroup.transform : null
            };
            int count = 0;

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i])
                {
                    pieces[count++] = pieces[i];
                }
            }

            _hintPieces = new RectTransform[count];
            _hintRestY = new float[count];

            for (int i = 0; i < count; i++)
            {
                _hintPieces[i] = pieces[i];
                _hintRestY[i] = pieces[i].anchoredPosition.y;
            }
        }

        // An icon and a copy of the job name, right-aligned in the role row, in one group that fades with the panel.
        private void BuildRoleMark()
        {
            if (!_roleName || !_rolePanel)
            {
                return;
            }

            RectTransform nameRect = _roleName.rectTransform;
            float rowHeight = nameRect.sizeDelta.y;
            float iconSize = rowHeight * 1.1f;
            float labelWidth = 80f;
            float width = iconSize + 6f + labelWidth;
            GameObject group = new GameObject("RoleMark", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform groupRect = (RectTransform)group.transform;
            groupRect.SetParent(nameRect.parent, false);
            groupRect.anchorMin = nameRect.anchorMin;
            groupRect.anchorMax = nameRect.anchorMax;
            groupRect.pivot = nameRect.pivot;
            groupRect.sizeDelta = new Vector2(width, rowHeight);
            float right = _rolePanel.anchoredPosition.x + (1f - _rolePanel.pivot.x) * _rolePanel.sizeDelta.x - _roleMarkInset;
            groupRect.anchoredPosition = new Vector2(right - width, nameRect.anchoredPosition.y);
            _roleGroup = group.GetComponent<CanvasGroup>();
            _roleGroup.interactable = false;
            _roleGroup.blocksRaycasts = false;

            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(groupRect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(iconSize * 0.5f, 0f);
            _roleMarkIcon = icon.GetComponent<Image>();
            _roleMarkIcon.preserveAspect = true;
            _roleMarkIcon.raycastTarget = false;

            _roleMarkLabel = Instantiate(_roleName, groupRect);
            _roleMarkLabel.name = "Label";
            RectTransform labelRect = _roleMarkLabel.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.sizeDelta = new Vector2(labelWidth, rowHeight);
            labelRect.anchoredPosition = Vector2.zero;
            _roleMarkLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _roleMarkLabel.enableAutoSizing = false;
            _roleMarkLabel.raycastTarget = false;

            group.SetActive(false);
            _shownRole = MatchRole.None;
        }

        // The folded rows move one row down under the Alt row and into one group that fades together.
        private CanvasGroup FoldHints()
        {
            if (_foldedHints.Length == 0 || !_exitLabel)
            {
                return null;
            }

            RectTransform parent = (RectTransform)_foldedHints[0].parent;
            float rowStep = _secondRowKey ? _exitLabel.rectTransform.anchoredPosition.y - _secondRowKey.anchoredPosition.y : 0f;
            _rowStep = rowStep;
            GameObject group = new GameObject("RevealHints", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform groupRect = (RectTransform)group.transform;
            groupRect.SetParent(parent, false);
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.pivot = parent.pivot;
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;

            for (int i = 0; i < _foldedHints.Length; i++)
            {
                RectTransform hint = _foldedHints[i];
                hint.SetParent(groupRect, false);
                hint.anchoredPosition -= new Vector2(0f, rowStep);
            }

            return group.GetComponent<CanvasGroup>();
        }

        private void AddKeyCap(Button button, string letter)
        {
            if (!button || !_voteKeyCap)
            {
                return;
            }

            GameObject cap = Instantiate(_voteKeyCap, button.transform, false);
            cap.name = "Key" + letter;
            Text text = cap.GetComponentInChildren<Text>(true);

            if (text)
            {
                text.text = letter;
            }
        }

        private static CanvasGroup GetGroup(GameObject target)
        {
            if (!target)
            {
                return null;
            }

            if (!target.TryGetComponent(out CanvasGroup group))
            {
                group = target.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private static void SetInteractable(CanvasGroup group, bool isOn)
        {
            if (!group)
            {
                return;
            }

            group.interactable = isOn;
            group.blocksRaycasts = isOn;
        }
    }
}
