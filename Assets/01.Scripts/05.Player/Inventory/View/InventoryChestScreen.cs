using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Network.Session.View;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lighthouse.Player.Inventory.View
{
    public sealed partial class InventoryChestScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _window;
        [SerializeField] private InventoryChestSlot[] _resourceSlots;
        [SerializeField] private int[] _resourceIds;
        [SerializeField] private Sprite[] _resourceIcons;
        [SerializeField] private InventoryChestSlot[] _moduleSlots;
        [SerializeField] private int[] _recipeIds;
        [SerializeField] private Sprite[] _recipeIcons;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _message;

        private NetworkIdentity _boundPlayer;
        private PlayerInventory _inventory;
        private PlayerController _player;
        private InventoryDebugPanel _debugPanel;
        private InteractionRelay _relay;
        private PlayerInput _input;
        private Font _font;
        private bool _isOpen;
        private bool _isInstalling;
        private bool _wasInputEnabled;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;
        private int _openedFrame;
        private int _displayedModuleId = -1;
        private int _selectedModule;

        private void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            foreach (Text text in _window.GetComponentsInChildren<Text>(true))
            {
                text.font = _font;
                text.fontStyle = FontStyle.Bold;
            }
            BuildTabbedLayout();
            BuildCarriedModuleHud();
            _window.SetActive(false);
        }

        private void OnEnable()
        {
            _closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            Close();
            BindPlayer(null);
            _carriedHud.gameObject.SetActive(false);
            _closeButton.onClick.RemoveListener(Close);
        }

        private void OnDestroy()
        {
            if (_font)
            {
                Destroy(_font);
            }
            DisposeLayout();
        }

        private void Update()
        {
            if (_boundPlayer != NetworkClient.localPlayer)
            {
                BindPlayer(NetworkClient.localPlayer);
            }
            UpdateCarriedModuleHud();
            if (!_isOpen)
            {
                return;
            }
            if (!_inventory || !_player || _player.IsDowned || !NetworkClient.isConnected)
            {
                Close();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (Time.frameCount != _openedFrame && keyboard != null
                && (keyboard.escapeKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame
                    || keyboard.tabKey.wasPressedThisFrame))
            {
                Close();
                return;
            }
            if (_displayedModuleId != _inventory.ModuleRecipeId)
            {
                RefreshModules();
            }
            UpdateShipPanels();
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
                return;
            }
            if (!_inventory || !_player || _player.IsDowned || SessionScreen.IsGameplayOverlayOpen)
            {
                return;
            }
            _isOpen = true;
            _carriedHud.gameObject.SetActive(false);
            _openedFrame = Time.frameCount;
            _selectedModule = 0;
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _wasInputEnabled = _input && _input.enabled;
            if (_input)
            {
                _input.enabled = false;
            }
            SessionScreen.SetGameplayOverlayOpen(this, true);
            _window.SetActive(true);
            _message.text = "";
            RefreshResources();
            RefreshModules();
            SelectTab(0);
            UpdateShipPanels(true);
            RefreshModuleButtons();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }
            _isOpen = false;
            _window.SetActive(false);
            SessionScreen.SetGameplayOverlayOpen(this, false);
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            if (_input && _wasInputEnabled)
            {
                _input.enabled = true;
            }
        }

        public void SelectModule(int index)
        {
            _selectedModule = index;
            _message.text = GetModuleName();
            RefreshModules();
        }

        private void BindPlayer(NetworkIdentity identity)
        {
            Close();
            if (_inventory)
            {
                _inventory.ResourceCountChanged -= HandleResourceChanged;
                _inventory.InventoryReset -= HandleInventoryReset;
            }
            if (_relay)
            {
                _relay.ResultReceived -= HandleInstallResult;
            }
            if (_debugPanel)
            {
                _debugPanel.BindChestView(null);
            }
            _boundPlayer = identity;
            _inventory = identity ? identity.GetComponent<PlayerInventory>() : null;
            _player = identity ? identity.GetComponent<PlayerController>() : null;
            _relay = identity ? identity.GetComponent<InteractionRelay>() : null;
            _input = identity ? identity.GetComponent<PlayerInput>() : null;
            _debugPanel = identity ? identity.GetComponent<InventoryDebugPanel>() : null;
            _isInstalling = false;
            _displayedModuleId = -1;
            _isRepairing = false;
            BindBoat(null);
            if (_inventory)
            {
                _inventory.ResourceCountChanged += HandleResourceChanged;
                _inventory.InventoryReset += HandleInventoryReset;
            }
            if (_relay)
            {
                _relay.ResultReceived += HandleInstallResult;
            }
            if (_debugPanel)
            {
                _debugPanel.BindChestView(this);
            }
        }

        private void HandleResourceChanged(int id, int count)
        {
            RefreshResources();
            UpdateShipPanels(true);
        }

        private void HandleInventoryReset()
        {
            // Mirror's clear callback runs before the old dictionary entries are removed.
            for (int i = 0; i < _resourceSlots.Length; i++)
            {
                _resourceSlots[i].SetContent(null, "");
            }
            _capacityLabel.text = $"0 / {InventoryData.SlotCapacity} 칸";
        }

        private void RefreshResources()
        {
            int slot = 0;
            int stackCapacity = _inventory ? Mathf.Max(1, _inventory.StackCapacity) : InventoryData.StackCapacity;
            for (int i = 0; i < _resourceIds.Length && slot < _resourceSlots.Length; i++)
            {
                int count = _inventory && _resourceIds[i] > 0 ? _inventory.GetSyncedCount(_resourceIds[i]) : 0;
                while (count > 0 && slot < _resourceSlots.Length)
                {
                    int stack = Mathf.Min(count, stackCapacity);
                    _resourceSlots[slot++].SetContent(_resourceIcons[i], stack.ToString());
                    count -= stack;
                }
            }
            while (slot < _resourceSlots.Length)
            {
                _resourceSlots[slot++].SetContent(null, "");
            }
            RefreshCapacity();
        }

        private void RefreshModules()
        {
            _displayedModuleId = _inventory ? _inventory.ModuleRecipeId : 0;
            Sprite icon = null;
            for (int i = 0; i < _recipeIds.Length; i++)
            {
                if (_recipeIds[i] == _displayedModuleId)
                {
                    icon = _recipeIcons[i];
                    break;
                }
            }
            _moduleSlots[0].SetContent(icon, "");
            _moduleSlots[0].SetSelected(_selectedModule == 0 && _displayedModuleId != 0);
            RefreshCarriedModule();
        }

        private string GetModuleName()
        {
            ModuleRecipe recipe = _inventory ? _inventory.GetModuleRecipe() : null;
            return recipe ? recipe.DisplayName : "모듈 없음";
        }

        private bool CanInstall()
        {
            ModuleRecipe recipe = _inventory ? _inventory.GetModuleRecipe() : null;
            return _selectedModule == 0 && recipe && recipe.IsBoatModule && _player && !_player.IsDowned
                && _player.IsOnBoat && _player.CurrentBoat && _player.CurrentBoat == _player.OwnedBoat
                && _player.CurrentBoat.netId != 0 && NetworkClient.ready && _relay
                && !_relay.IsAwaitingResult && !_isInstalling;
        }

        private void HandleInstallClicked()
        {
            if (!CanInstall())
            {
                return;
            }
            _isInstalling = true;
            _message.text = "장착 요청 중...";
            if (!_relay.TrySendToNetworkTarget(_player.CurrentBoat.netId,
                InteractionAction.InstallModule, _inventory.ModuleRecipeId))
            {
                _isInstalling = false;
                _message.text = "요청을 보내지 못했습니다.";
            }
        }

        private void HandleInstallResult(InteractionResult result)
        {
            if (_isRepairing)
            {
                _isRepairing = false;
                _message.text = result.IsSuccess ? "재료를 투입했습니다. 할당량이 모이면 자동으로 수리됩니다." : $"수리 요청 거절: {result.Code}";
                UpdateShipPanels(true);
                return;
            }
            if (!_isInstalling)
            {
                return;
            }
            _isInstalling = false;
            _message.text = result.IsSuccess ? "배에 모듈을 장착했습니다." : $"장착 거절: {result.Code}";
            RefreshModules();
            UpdateShipPanels(true);
        }
    }
}
