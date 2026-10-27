using System;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Session.View;
using Lighthouse.Stations.View;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Rules;
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

        private NetworkIdentity _player;
        private InteractionRelay _relay;
        private PlayerInput _playerInput;
        private bool _isOpen;
        private bool _isInRange;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        private bool _isPending;
        private int _pendingSlot = -1;
        private string _pendingName;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _installButton.onClick.AddListener(HandleInstallClicked);
            _window.SetActive(false);
            _prompt.SetActive(false);
        }

        private void OnEnable()
        {
            if (_tower)
            {
                _tower.Changed += HandleChanged;

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

            // The workbench shares the open key and stands within reach, so only the nearer one answers it.
            bool isFocused = _isInRange && StationScreenFocus.IsNearest(this);
            _prompt.SetActive(isFocused && !_isOpen && !SessionScreen.IsGameplayOverlayOpen);
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame))
            {
                Close();
            }
            else if (_isOpen && keyboard[_installKey].wasPressedThisFrame)
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

                if (_tower.Workbench)
                {
                    _tower.Workbench.Changed -= HandleChanged;
                }
            }

            StationScreenFocus.Unregister(this);
            Close();
            BindLocalPlayer(null);
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
            _window.SetActive(true);
            _prompt.SetActive(false);
            SetMessage(string.Empty, _messageSuccessColor);
            LockPlayer(true);
            Refresh();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _window.SetActive(false);
            LockPlayer(false);
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

                if (_playerInput)
                {
                    _wasPlayerInputEnabled = _playerInput.enabled;
                    _playerInput.enabled = false;
                }

                return;
            }

            if (_hasSavedCursor)
            {
                _hasSavedCursor = false;
                Cursor.lockState = _savedLockMode;
                Cursor.visible = _savedCursorVisible;
            }

            if (_playerInput && _wasPlayerInputEnabled)
            {
                _playerInput.enabled = true;
            }
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
                TowerSlotView.State state = _tower.IsInstalled(i) ? TowerSlotView.State.Installed
                    : i == targetSlot ? TowerSlotView.State.Target
                    : _tower.IsFloorOpen(slot.Floor) ? TowerSlotView.State.Open
                    : TowerSlotView.State.Locked;
                _slots[i].gameObject.SetActive(true);
                _slots[i].Show(GetRecipeIcon(slot.Recipe), state);
            }

            int installed = _tower.InstalledCount;
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
            Refresh();
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (!_isPending)
            {
                return;
            }

            _isPending = false;

            if (!_isOpen)
            {
                return;
            }

            if (result.IsSuccess)
            {
                string floor = _tower.Layout.GetFloorName(_tower.Layout[_pendingSlot].Floor);
                SetMessage($"{_pendingName}을(를) {floor}에 장착했습니다.", _messageSuccessColor);
            }
            else
            {
                SetMessage(DescribeRejection(result.Code), _messageRejectColor);
            }

            Refresh();
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
