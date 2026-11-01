using System;
using System.Collections.Generic;
using Lighthouse.Common.UI;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Session.View;
using Lighthouse.Stations.View;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Rules;
using Lighthouse.Workbench.View;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // Install screen for the lighthouse (2026-10-21 sketch). Opens with the open key inside the lighthouse's interaction
    // range and shows every slot, the module this player carries and the repair count. The install key sends the
    // same InstallModule request as any other input; the server picks the slot, so the highlighted slot is only the
    // answer TowerNet.FindOpenSlot gives every machine. What this player carries comes from its own TargetRpc.
    [DisallowMultipleComponent]
    public sealed class TowerScreen : MonoBehaviour, IStationScreen
    {
        [Serializable]
        private struct RecipeIcon
        {
            public ModuleRecipe Recipe;
            public Sprite Icon;
        }

        [SerializeField]
        private TowerNet _tower;
        [SerializeField]
        private InteractionTarget _target;
        [SerializeField]
        private Key _openKey = Key.E;
        [SerializeField]
        private Key _installKey = Key.F;
        [Tooltip("Optional: the camera moves to the lighthouse before the screen shows. Without it the screen opens at once.")]
        [SerializeField]
        private TowerFocusCamera _focusCamera;

        [Header("Screen")]
        [SerializeField]
        private GameObject _window;
        [SerializeField]
        private GameObject _prompt;
        [SerializeField]
        private Text _message;
        [SerializeField]
        private Color _messageSuccessColor = new Color(0.98f, 0.92f, 0.79f);
        [SerializeField]
        private Color _messageRejectColor = new Color(0.92f, 0.45f, 0.38f);

        [Header("Tower")]
        [Tooltip("Indexed like the layout's slots.")]
        [SerializeField]
        private TowerSlotView[] _slots = new TowerSlotView[0];
        [Tooltip("Indexed like the layout's floors, bottom first.")]
        [SerializeField]
        private Text[] _floorLabels = new Text[0];
        [SerializeField]
        private RectTransform _progressFill;
        [SerializeField]
        private Text _progressText;
        [SerializeField]
        private RecipeIcon[] _recipeIcons = new RecipeIcon[0];

        [Header("Carried module")]
        [SerializeField]
        private Image _carryIcon;
        [SerializeField]
        private Button _installButton;
        [SerializeField]
        private Image _installBackground;
        [SerializeField]
        private Text _installText;
        [SerializeField]
        private CanvasGroup _installKeycap;
        [Header("Ritual")]
        [SerializeField]
        private Button _ritualButton;
        [SerializeField]
        private Image _ritualBackground;
        [SerializeField]
        private Text _ritualText;
        [SerializeField]
        private Sprite _installReadySprite;
        [SerializeField]
        private Sprite _installBlockedSprite;
        [SerializeField]
        private Color _installReadyTextColor = new Color(0.12f, 0.09f, 0.05f);
        [SerializeField]
        private Color _installBlockedTextColor = new Color(0.55f, 0.51f, 0.43f);
        [SerializeField, Range(0f, 1f)]
        private float _blockedKeycapAlpha = 0.35f;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        [Header("Effects (2026-10-27): install flight, slot click, floor sweep, count")]
        [SerializeField]
        private WorkbenchScreenEffects.Settings _effectSettings = new WorkbenchScreenEffects.Settings();

        private NetworkIdentity _player;
        private InteractionRelay _relay;
        private PlayerInput _playerInput;
        private bool _isOpen;
        private bool _isInRange;
        private bool _isPlayerLocked;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        // The camera returns after the screen closes, so the unlock may come after the local player was rebound.
        private PlayerInput _lockedInput;
        private bool _isPending;
        private int _pendingSlot = -1;
        private string _pendingName;
        private WorkbenchScreenEffects _effects;
        private Canvas _canvas;
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        // This player's install, on its own screen only: from the click the slot keeps waiting and the count stays,
        // until the module icon has flown in from the carried panel (the host hears of the slot before the result).
        private int _landingSlot = -1;
        private float _landingAt;
        private Vector3 _installFrom;
        private Sprite _installIcon;
        private TowerMatchNet _match;
        private bool _isRitualPending;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _canvas = GetComponent<Canvas>();
            _installButton.onClick.AddListener(HandleInstallClicked);
            _match = _tower ? _tower.GetComponent<TowerMatchNet>() : null;
            if (_ritualButton)
            {
                _ritualButton.onClick.AddListener(HandleRitualClicked);
            }
            _effects = new WorkbenchScreenEffects(_effectSettings, (RectTransform)_window.transform, _message.font, null, null);
            _window.SetActive(false);
            _prompt.SetActive(false);
        }

        private void OnEnable()
        {
            if (_tower)
            {
                _tower.Changed += HandleChanged;
                _tower.SlotInstalled += HandleSlotInstalled;

                if (_tower.Workbench)
                {
                    _tower.Workbench.Changed += HandleChanged;
                }
            }

            StationScreenFocus.Register(this);
        }

        private void Update()
        {
            if (_player != NetworkClient.localPlayer)
            {
                BindLocalPlayer(NetworkClient.localPlayer);
            }

            bool isInRange = IsLocalPlayerInRange();

            if (isInRange != _isInRange)
            {
                _isInRange = isInRange;

                if (!isInRange && _isOpen)
                {
                    Close();
                }
            }

            if (_isOpen)
            {
                _effects.Tick(Time.unscaledDeltaTime);
                TickLanding();
                RefreshRitualButton();
            }

            // The workbench shares the open key and stands within reach, so only the nearer one answers it.
            bool isFocused = _isInRange && StationScreenFocus.IsNearest(this);
            _prompt.SetActive(isFocused && !_isOpen && !_isPlayerLocked && !SessionScreen.IsGameplayOverlayOpen);
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame))
            {
                Close();
            }
            else if (_isOpen && _window.activeSelf && keyboard[_installKey].wasPressedThisFrame)
            {
                HandleInstallClicked();
            }
            else if (!_isOpen && isFocused && keyboard[_openKey].wasPressedThisFrame && !SessionScreen.IsGameplayOverlayOpen)
            {
                Open();
            }
        }

        private void OnDisable()
        {
            if (_tower)
            {
                _tower.Changed -= HandleChanged;
                _tower.SlotInstalled -= HandleSlotInstalled;

                if (_tower.Workbench)
                {
                    _tower.Workbench.Changed -= HandleChanged;
                }
            }

            StationScreenFocus.Unregister(this);
            Close();
            BindLocalPlayer(null);
        }

        public void Bind(TowerNet tower, InteractionTarget target)
        {
            Close();

            if (isActiveAndEnabled && _tower)
            {
                _tower.Changed -= HandleChanged;
                if (_tower.Workbench)
                {
                    _tower.Workbench.Changed -= HandleChanged;
                }
            }

            _tower = tower;
            _match = tower ? tower.GetComponent<TowerMatchNet>() : null;
            _target = target;
            _isInRange = false;
            _isPending = false;
            _isRitualPending = false;
            _prompt.SetActive(false);

            if (isActiveAndEnabled && _tower)
            {
                _tower.Changed += HandleChanged;
                if (_tower.Workbench)
                {
                    _tower.Workbench.Changed += HandleChanged;
                }
            }
        }

        public void Open()
        {
            if (_isOpen || !_tower || !_tower.Layout || !_relay)
            {
                return;
            }

            _isOpen = true;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            _isPending = false;
            _isRitualPending = false;
            _prompt.SetActive(false);
            SetMessage(string.Empty, _messageSuccessColor);
            LockPlayer(true);
            HideOtherScreens(true);

            // Esc or the open key can still back out during the move; installing waits for the window.
            if (!_focusCamera || !_focusCamera.Focus(_tower.transform, ShowWindow))
            {
                ShowWindow();
            }
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _effects.StopAll();
            _landingSlot = -1;
            _landingAt = 0f;
            _window.SetActive(false);

            if (_focusCamera)
            {
                _focusCamera.Return(HandleViewReturned);
            }
            else
            {
                HandleViewReturned();
            }
        }

        private void ShowWindow()
        {
            if (!_isOpen)
            {
                return;
            }

            _window.SetActive(true);
            Refresh();
        }

        private void HandleViewReturned()
        {
            LockPlayer(false);
            HideOtherScreens(false);
        }

        // Every other screen-space canvas steps aside while this screen holds the view (2026-10-30), so only the
        // lighthouse and this screen show; they come back with the player's camera. Inactive canvases are left alone.
        private void HideOtherScreens(bool isHidden)
        {
            if (!isHidden)
            {
                for (int i = 0; i < _hiddenCanvases.Count; i++)
                {
                    if (_hiddenCanvases[i])
                    {
                        _hiddenCanvases[i].enabled = true;
                    }
                }

                _hiddenCanvases.Clear();
                return;
            }

            // Reopened while the camera was on its way back: those canvases are still hidden.
            if (_hiddenCanvases.Count > 0)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];

                // Notices and match effects answer what happens on this screen (an install, a finished floor), so
                // they stay: hidden, a notice would run out its time unseen.
                if (canvas == _canvas || !canvas.enabled || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace
                    || canvas.TryGetComponent(out ScreenNotice _) || canvas.TryGetComponent(out TowerMatchEffects _))
                {
                    continue;
                }

                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        private void BindLocalPlayer(NetworkIdentity player)
        {
            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }

            _player = player;
            _relay = player ? player.GetComponent<InteractionRelay>() : null;
            _playerInput = player ? player.GetComponent<PlayerInput>() : null;

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }
        }

        public bool TryGetLocalSqrDistance(out float sqrDistance)
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            sqrDistance = 0f;

            if (!player || !_target || !_tower || !_tower.isActiveAndEnabled)
            {
                return false;
            }

            float range = _target.Range;
            sqrDistance = (player.transform.position - _target.Position).sqrMagnitude;
            return sqrDistance <= range * range;
        }

        private bool IsLocalPlayerInRange()
        {
            if (!_player || !_target || !_tower || !_tower.isActiveAndEnabled)
            {
                return false;
            }

            float range = _target.Range;
            return (_player.transform.position - _target.Position).sqrMagnitude <= range * range;
        }

        // Same stand-in input lock as the workbench screen until the player side has one.
        private void LockPlayer(bool isLocked)
        {
            // Reopening while the camera is still on its way back keeps the lock it already holds.
            if (isLocked == _isPlayerLocked)
            {
                return;
            }

            _isPlayerLocked = isLocked;

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

                _lockedInput = _playerInput;

                if (_lockedInput)
                {
                    _wasPlayerInputEnabled = _lockedInput.enabled;
                    _lockedInput.enabled = false;
                }

                return;
            }

            if (_hasSavedCursor)
            {
                _hasSavedCursor = false;
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }

            if (_lockedInput && _wasPlayerInputEnabled)
            {
                _lockedInput.enabled = true;
            }

            _lockedInput = null;
        }

        private void Refresh()
        {
            if (!_isOpen)
            {
                return;
            }

            TowerLayout layout = _tower.Layout;
            int carriedId = GetCarriedRecipeId();
            int targetSlot = _tower.FindOpenSlot(carriedId);

            for (int i = 0; i < _floorLabels.Length; i++)
            {
                _floorLabels[i].text = layout.GetFloorName(i);
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i >= layout.SlotCount)
                {
                    _slots[i].gameObject.SetActive(false);
                    continue;
                }

                TowerSlot slot = layout[i];
                TowerSlotView.State state = i == _landingSlot ? TowerSlotView.State.Target
                    : _tower.IsInstalled(i) ? TowerSlotView.State.Installed
                    : i == targetSlot ? TowerSlotView.State.Target
                    : _tower.IsFloorOpen(slot.Floor) ? TowerSlotView.State.Open
                    : TowerSlotView.State.Locked;
                _slots[i].gameObject.SetActive(true);
                _slots[i].Show(GetRecipeIcon(slot.Recipe), state);
            }

            int installed = _tower.InstalledCount - (_landingSlot >= 0 && _tower.IsInstalled(_landingSlot) ? 1 : 0);
            _progressText.text = $"{installed}/{layout.SlotCount}";
            _progressFill.anchorMax = new Vector2(layout.GetRepairRatio(installed), 1f);

            ModuleRecipe carried = FindCarriedRecipe(carriedId);
            _carryIcon.sprite = carried ? GetRecipeIcon(carried) : null;
            _carryIcon.enabled = _carryIcon.sprite;

            // Empty hands, a boat module or a module whose slot is still locked all leave nothing to install.
            bool canInstall = targetSlot >= 0 && !_isPending;
            _installButton.interactable = canInstall;
            _installBackground.sprite = canInstall ? _installReadySprite : _installBlockedSprite;
            _installText.color = canInstall ? _installReadyTextColor : _installBlockedTextColor;
            _installKeycap.alpha = canInstall ? 1f : _blockedKeycapAlpha;
            RefreshRitualButton();
        }

        private void RefreshRitualButton()
        {
            if (!_ritualButton)
            {
                return;
            }

            bool canStart = _match && _match.CanStartRitual && _relay && !_isPending;
            _ritualButton.interactable = canStart;
            _ritualBackground.sprite = canStart ? _installReadySprite : _installBlockedSprite;
            _ritualText.color = canStart ? _installReadyTextColor : _installBlockedTextColor;
        }

        private void HandleRitualClicked()
        {
            if (!_isOpen || !_window.activeSelf || _isPending || !_match || !_match.CanStartRitual || !_relay)
            {
                return;
            }

            if (!_relay.TrySendToNetworkTarget(_match.netId, InteractionAction.StartRitual))
            {
                SetMessage("이전 요청을 처리하는 중입니다. 잠시 후 다시 시도하세요.", _messageRejectColor);
                return;
            }

            _isRitualPending = true;
            _isPending = true;
            Refresh();
        }

        private int GetCarriedRecipeId()
        {
            return _tower.Workbench ? _tower.Workbench.OwnCarriedRecipe : 0;
        }

        private ModuleRecipe FindCarriedRecipe(int recipeId)
        {
            ModuleRecipeCatalog catalog = _tower.Workbench ? _tower.Workbench.Catalog : null;
            return recipeId != 0 && catalog && catalog.TryGetById(recipeId, out ModuleRecipe recipe) ? recipe : null;
        }

        private Sprite GetRecipeIcon(ModuleRecipe recipe)
        {
            if (!recipe)
            {
                return null;
            }

            for (int i = 0; i < _recipeIcons.Length; i++)
            {
                if (_recipeIcons[i].Recipe == recipe && _recipeIcons[i].Icon)
                {
                    return _recipeIcons[i].Icon;
                }
            }

            return recipe.Icon;
        }

        private void HandleInstallClicked()
        {
            int carriedId = GetCarriedRecipeId();
            int slot = _tower.FindOpenSlot(carriedId);

            if (!_isOpen || _isPending || slot < 0)
            {
                return;
            }

            if (!_relay.TrySendToNetworkTarget(_tower.netId, InteractionAction.InstallModule, carriedId))
            {
                SetMessage("이전 요청을 처리하는 중입니다. 잠시 후 다시 시도하세요.", _messageRejectColor);
                return;
            }

            ModuleRecipe recipe = FindCarriedRecipe(carriedId);
            _isPending = true;
            _pendingSlot = slot;
            _pendingName = recipe ? recipe.DisplayName : "모듈";
            _landingSlot = slot;
            _landingAt = 0f;
            _installFrom = WorkbenchScreenEffects.CenterOf(_carryIcon.rectTransform);
            _installIcon = GetRecipeIcon(recipe);
            Refresh();
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (!_isPending)
            {
                return;
            }

            _isPending = false;

            if (_isRitualPending)
            {
                _isRitualPending = false;
                if (result.IsSuccess)
                {
                    Close();
                }
                else if (_isOpen)
                {
                    SetMessage(DescribeRejection(result.Code), _messageRejectColor);
                    Refresh();
                }

                return;
            }

            if (!_isOpen || !result.IsSuccess)
            {
                _landingSlot = -1;
            }

            if (!_isOpen)
            {
                return;
            }

            if (result.IsSuccess)
            {
                string floor = _tower.Layout.GetFloorName(_tower.Layout[_pendingSlot].Floor);
                SetMessage($"{_pendingName}을(를) {floor}에 장착했습니다.", _messageSuccessColor);
                _effects.PlayFlight(_installIcon, _installFrom, _slots[_landingSlot].IconRect, null, WorkbenchEffectCue.ModuleStored);
                _landingAt = Time.unscaledTime + _effectSettings.FlightSeconds;
            }
            else
            {
                SetMessage(DescribeRejection(result.Code), _messageRejectColor);
            }

            Refresh();
        }

        // The slot fills when the icon lands. A remote client may still be waiting for the slot itself, so it waits a
        // little longer for that rather than letting the slot's own change play as someone else's install.
        private void TickLanding()
        {
            if (_landingAt <= 0f || Time.unscaledTime < _landingAt)
            {
                return;
            }

            int slot = _landingSlot;

            if (!_tower.IsInstalled(slot) && Time.unscaledTime < _landingAt + _effectSettings.CraftWaitSeconds)
            {
                return;
            }

            _landingSlot = -1;
            _landingAt = 0f;
            Refresh();

            if (_tower.IsInstalled(slot))
            {
                PlayInstalled(slot);
            }
        }

        // Another crew member's install while this screen is open, or this player's once it lands.
        private void HandleSlotInstalled(int slot)
        {
            if (_isOpen && slot != _landingSlot && slot >= 0 && slot < _slots.Length)
            {
                PlayInstalled(slot);
            }
        }

        // The slot clicks in with a glow and the count jumps; a finished floor gets a sweep and its line.
        private void PlayInstalled(int slot)
        {
            TowerSlotView view = _slots[slot];
            _effects.PlayPunch((RectTransform)view.transform, _effectSettings.BadgePunchStrength);
            _effects.PlayFlash(view.Frame, _effectSettings.HighlightColor, 1);
            _effects.PlayPunch(_progressText.rectTransform, _effectSettings.BadgePunchStrength);

            TowerLayout layout = _tower.Layout;
            int floor = layout[slot].Floor;

            if (!_tower.IsFloorComplete(floor))
            {
                return;
            }

            RectTransform first = null;
            RectTransform last = null;

            for (int i = 0; i < layout.SlotCount && i < _slots.Length; i++)
            {
                if (layout[i].Floor == floor)
                {
                    first = first ? first : (RectTransform)_slots[i].transform;
                    last = (RectTransform)_slots[i].transform;
                }
            }

            if (!last)
            {
                return;
            }

            RectTransform start = floor < _floorLabels.Length ? _floorLabels[floor].rectTransform : first;
            Vector3 from = start.TransformPoint(new Vector3(start.rect.xMin, start.rect.center.y, 0f));
            Vector3 to = last.TransformPoint(new Vector3(last.rect.xMax, last.rect.center.y, 0f));
            _effects.PlaySweep(from, to, last.rect.height * 1.3f);
            _effects.PlayText((from + to) * 0.5f, $"{layout.GetFloorName(floor)} 수리 완료");
        }

        private string DescribeRejection(InteractionResultCode code)
        {
            switch (code)
            {
                case InteractionResultCode.OutOfRange:
                    return "등대에서 너무 멉니다.";
                case InteractionResultCode.NotInProgress:
                    return "판이 진행 중일 때만 장착할 수 있습니다.";
                case InteractionResultCode.Rejected:
                    return "장착할 수 없습니다. (다른 선원이 먼저 장착함, 장착 마감, 쓰러짐·배 탑승 상태)";
                default:
                    return $"요청을 처리하지 못했습니다. ({code})";
            }
        }

        private void HandleChanged()
        {
            Refresh();
        }

        private void SetMessage(string text, Color color)
        {
            _message.text = text;
            _message.color = color;
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
