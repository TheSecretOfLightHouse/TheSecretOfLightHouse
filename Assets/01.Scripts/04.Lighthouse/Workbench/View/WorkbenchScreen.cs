using System;
using System.Collections.Generic;
using System.Text;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // Canvas screen for the shared workbench (JH03 draft until MK06 owns the final UI). Opens with the open key
    // while the local player is inside the workbench's interaction range, and sends the same relay requests as
    // real input would. Everything shown comes from WorkbenchNet's public state and the local inventory.
    [DisallowMultipleComponent]
    public sealed class WorkbenchScreen : MonoBehaviour
    {
        private const string IdleMessage = "레시피를 고르고 자원을 넣으세요. 넣은 자원은 되돌릴 수 없습니다.";

        [Serializable]
        private struct ResourceIcon
        {
            public ResourceDefinition Resource;
            public Sprite Icon;
        }

        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private InteractionTarget _target;
        [SerializeField]
        private Key _openKey = Key.F;

        [Header("Screen")]
        [SerializeField]
        private GameObject _window;
        [SerializeField]
        private GameObject _prompt;
        [SerializeField]
        private Text _promptText;
        [SerializeField]
        private Button[] _closeButtons = new Button[0];
        [SerializeField]
        private Text _message;
        [SerializeField]
        private Color _messageInfoColor = new Color(0.62f, 0.67f, 0.75f);
        [SerializeField]
        private Color _messageSuccessColor = new Color(0.55f, 0.82f, 0.55f);
        [SerializeField]
        private Color _messageRejectColor = new Color(0.9f, 0.5f, 0.45f);

        [Header("Recipes")]
        [SerializeField]
        private WorkbenchRecipeCard _cardTemplate;
        [SerializeField]
        private Sprite _recipeFallbackIcon;

        [Header("Progress")]
        [SerializeField]
        private Text _progressTitle;
        [SerializeField]
        private GameObject _progressEmpty;
        [SerializeField]
        private WorkbenchIngredientRow _rowTemplate;
        [SerializeField]
        private Text _progressHint;
        [SerializeField]
        private Button _craftButton;

        [Header("First deposit confirm")]
        [SerializeField]
        private GameObject _confirm;
        [SerializeField]
        private Text _confirmText;
        [SerializeField]
        private Button _confirmYesButton;
        [SerializeField]
        private Button _confirmNoButton;

        [Header("Stock")]
        [SerializeField]
        private WorkbenchItemSlot[] _resourceSlots = new WorkbenchItemSlot[0];
        [SerializeField]
        private WorkbenchItemSlot[] _storedSlots = new WorkbenchItemSlot[0];
        [SerializeField]
        private ResourceIcon[] _resourceIcons = new ResourceIcon[0];

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<WorkbenchRecipeCard> _cards = new List<WorkbenchRecipeCard>();
        private readonly List<WorkbenchIngredientRow> _rows = new List<WorkbenchIngredientRow>();
        private readonly StringBuilder _builder = new StringBuilder();
        private NetworkIdentity _player;
        private InteractionRelay _relay;
        private PlayerInventory _inventory;
        private PlayerInput _playerInput;
        private bool _isOpen;
        private bool _isInRange;
        private bool _hasSavedCursor;
        private CursorLockMode _savedLockMode;
        private bool _savedCursorVisible;
        private bool _wasPlayerInputEnabled;
        private InteractionAction _pendingAction;
        private int _pendingArg;
        private string _pendingRecipeName;
        private int _confirmResourceId;
        private bool _isConfirmAll;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_useOsFont)
            {
                ApplyOsFont();
            }

            _cardTemplate.gameObject.SetActive(false);
            _rowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < _closeButtons.Length; i++)
            {
                _closeButtons[i].onClick.AddListener(Close);
            }

            _craftButton.onClick.AddListener(HandleCraftClicked);
            _confirmYesButton.onClick.AddListener(HandleConfirmYesClicked);
            _confirmNoButton.onClick.AddListener(HandleConfirmNoClicked);
            _window.SetActive(false);
            _prompt.SetActive(false);
            _confirm.SetActive(false);
        }

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.Changed += HandleWorkbenchChanged;
            }
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

            _prompt.SetActive(_isInRange && !_isOpen);
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (_isOpen && _confirm.activeSelf && keyboard.escapeKey.wasPressedThisFrame)
            {
                _confirm.SetActive(false);
            }
            else if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame))
            {
                Close();
            }
            else if (!_isOpen && _isInRange && keyboard[_openKey].wasPressedThisFrame)
            {
                Open();
            }
        }

        private void OnDisable()
        {
            if (_workbench)
            {
                _workbench.Changed -= HandleWorkbenchChanged;
            }

            Close();
            BindLocalPlayer(null);
        }

        public void Open()
        {
            if (_isOpen || !_workbench || !_workbench.Catalog || !_relay)
            {
                return;
            }

            _isOpen = true;
            EnsureEventSystem();
            BuildCards();
            _window.SetActive(true);
            _prompt.SetActive(false);
            SetMessage(IdleMessage, _messageInfoColor);
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
            _confirm.SetActive(false);
            _window.SetActive(false);
            LockPlayer(false);
        }

        private void BindLocalPlayer(NetworkIdentity player)
        {
            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }

            if (_inventory)
            {
                _inventory.ResourceCountChanged -= HandleResourceCountChanged;
            }

            _player = player;
            _relay = player ? player.GetComponent<InteractionRelay>() : null;
            _inventory = player ? player.GetComponent<PlayerInventory>() : null;
            _playerInput = player ? player.GetComponent<PlayerInput>() : null;

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }

            if (_inventory)
            {
                _inventory.ResourceCountChanged += HandleResourceCountChanged;
            }
        }

        private bool IsLocalPlayerInRange()
        {
            if (!_player || !_target || !_workbench || !_workbench.isActiveAndEnabled)
            {
                return false;
            }

            float range = _target.Range;
            return (_player.transform.position - _target.Position).sqrMagnitude <= range * range;
        }

        // Movement, camera look and harvest clicks all come from the player's actions, so they pause while the
        // screen is open. A proper input lock belongs to the player side (MK06); this is the draft's stand-in.
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

        private void BuildCards()
        {
            ModuleRecipeCatalog catalog = _workbench.Catalog;

            while (_cards.Count < catalog.Count)
            {
                WorkbenchRecipeCard card = Instantiate(_cardTemplate, _cardTemplate.transform.parent);
                _cards.Add(card);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                bool isUsed = i < catalog.Count;
                _cards[i].gameObject.SetActive(isUsed);

                if (isUsed)
                {
                    ModuleRecipe recipe = catalog[i];
                    _cards[i].Bind(recipe, DescribeIngredients(recipe), _recipeFallbackIcon, HandleRecipeClicked);
                }
            }
        }

        private void Refresh()
        {
            if (!_isOpen)
            {
                return;
            }

            bool hasRecipe = _workbench.TryGetCurrentRecipe(out ModuleRecipe current);
            bool isLocked = hasRecipe && HasAnyDeposit(current);
            RefreshCards(current, isLocked);
            RefreshProgress(hasRecipe ? current : null, isLocked);
            RefreshStock();
        }

        private void RefreshCards(ModuleRecipe current, bool isLocked)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                WorkbenchRecipeCard card = _cards[i];
                ModuleRecipe recipe = card.Recipe;

                if (!card.gameObject.activeSelf || !recipe)
                {
                    continue;
                }

                bool isSelected = recipe == current;
                bool isCapped = recipe.HasExistenceLimit && _workbench.GetStoredCount(recipe.Id) >= recipe.MaxExisting;
                string badge = isSelected ? (isLocked ? "투입 시작 · 변경 불가" : "선택됨") : (isCapped ? "보유 한도" : null);
                card.SetState(isSelected, !isSelected && !isLocked && !isCapped, badge);
            }
        }

        private void RefreshProgress(ModuleRecipe recipe, bool isLocked)
        {
            _progressEmpty.SetActive(!recipe);
            _progressTitle.text = recipe ? $"진행 중: {recipe.DisplayName}" : "진행 중인 레시피 없음";
            bool isReady = recipe && _workbench.IsReadyToCraft();
            _progressHint.gameObject.SetActive(recipe);
            _progressHint.text = isReady ? "재료가 모두 모였습니다. 제작을 누르면 완성됩니다."
                : isLocked ? "자원 투입이 시작되어 완성할 때까지 레시피를 바꿀 수 없습니다."
                : "아직 레시피를 바꿀 수 있습니다. 자원을 넣으면 고정됩니다.";
            _craftButton.gameObject.SetActive(recipe);
            _craftButton.interactable = isReady;

            if (!recipe || isLocked)
            {
                _confirm.SetActive(false);
            }
            int count = recipe ? recipe.IngredientCount : 0;

            while (_rows.Count < count)
            {
                _rows.Add(Instantiate(_rowTemplate, _rowTemplate.transform.parent));
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool isUsed = i < count;
                _rows[i].gameObject.SetActive(isUsed);

                if (!isUsed)
                {
                    continue;
                }

                RecipeIngredient ingredient = recipe.GetIngredient(i);
                ResourceDefinition resource = ingredient.Resource;
                _rows[i].Bind(ingredient.ResourceId, resource ? resource.DisplayName : "?", GetResourceIcon(resource), HandleDepositClicked);
                _rows[i].SetProgress(_workbench.GetDepositedAt(i), ingredient.Amount, Held(ingredient.ResourceId));
            }
        }

        private void RefreshStock()
        {
            for (int i = 0; i < _resourceSlots.Length; i++)
            {
                if (i < _resourceIcons.Length && _resourceIcons[i].Resource)
                {
                    ResourceDefinition resource = _resourceIcons[i].Resource;
                    _resourceSlots[i].Show(GetResourceIcon(resource), resource.DisplayName, Held(resource.Id));
                }
                else
                {
                    _resourceSlots[i].Clear();
                }
            }

            ModuleRecipeCatalog catalog = _workbench.Catalog;
            int slot = 0;

            for (int i = 0; i < catalog.Count && slot < _storedSlots.Length; i++)
            {
                int stored = _workbench.GetStoredCount(catalog[i].Id);

                if (stored > 0)
                {
                    ModuleRecipe recipe = catalog[i];
                    _storedSlots[slot++].Show(recipe.Icon ? recipe.Icon : _recipeFallbackIcon, recipe.DisplayName, stored);
                }
            }

            for (; slot < _storedSlots.Length; slot++)
            {
                _storedSlots[slot].Clear();
            }
        }

        private void Send(InteractionAction action, int arg0, int arg1)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1))
            {
                SetMessage("이전 요청을 처리하는 중입니다. 잠시 후 다시 시도하세요.", _messageRejectColor);
                return;
            }

            _pendingAction = action;
            _pendingArg = arg0;
        }

        private void HandleRecipeClicked(ModuleRecipe recipe)
        {
            Send(InteractionAction.SelectRecipe, recipe.Id, 0);
        }

        private void HandleDepositClicked(int resourceId, bool isAll)
        {
            // The first unit fixes the recipe for good, so that one deposit asks first.
            if (_workbench.TryGetCurrentRecipe(out ModuleRecipe recipe) && !HasAnyDeposit(recipe))
            {
                _confirmResourceId = resourceId;
                _isConfirmAll = isAll;
                _confirmText.text = $"자원을 넣으면 <b>{recipe.DisplayName}</b>을(를) 완성할 때까지\n레시피를 바꿀 수 없습니다. 넣을까요?";
                _confirm.SetActive(true);
                return;
            }

            SendDeposit(resourceId, isAll);
        }

        private void SendDeposit(int resourceId, bool isAll)
        {
            int amount = isAll ? _workbench.GetRemaining(resourceId) : 1;
            Send(InteractionAction.Deposit, resourceId, Mathf.Max(1, amount));
        }

        private void HandleConfirmYesClicked()
        {
            _confirm.SetActive(false);
            SendDeposit(_confirmResourceId, _isConfirmAll);
        }

        private void HandleConfirmNoClicked()
        {
            _confirm.SetActive(false);
        }

        private void HandleCraftClicked()
        {
            _pendingRecipeName = _workbench.TryGetCurrentRecipe(out ModuleRecipe recipe) ? recipe.DisplayName : string.Empty;
            Send(InteractionAction.Craft, 0, 0);
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (!_isOpen)
            {
                return;
            }

            if (result.IsSuccess)
            {
                SetMessage(DescribeSuccess(), _messageSuccessColor);
                return;
            }

            SetMessage(DescribeRejection(result.Code), _messageRejectColor);
        }

        private string DescribeRejection(InteractionResultCode code)
        {
            switch (code)
            {
                case InteractionResultCode.OutOfRange:
                    return "작업대에서 너무 멉니다.";
                case InteractionResultCode.NotInProgress:
                    return "판이 진행 중일 때만 쓸 수 있습니다.";
                case InteractionResultCode.Rejected:
                    break;
                default:
                    return $"요청을 처리하지 못했습니다. ({code})";
            }

            if (_pendingAction == InteractionAction.Craft)
            {
                return "제작할 수 없습니다. (재료가 모자라거나 다른 선원이 먼저 제작함, 쓰러짐·배 탑승 상태)";
            }

            if (_pendingAction == InteractionAction.SelectRecipe)
            {
                return _workbench.TryGetCurrentRecipe(out ModuleRecipe current) && HasAnyDeposit(current)
                    ? "이미 자원을 넣기 시작해 레시피를 바꿀 수 없습니다."
                    : "이 레시피는 지금 고를 수 없습니다. (보유 한도 또는 쓰러짐·배 탑승 상태)";
            }

            return Held(_pendingArg) <= 0
                ? "넣을 자원이 없습니다."
                : "넣을 수 없습니다. (다 찬 재료이거나 쓰러짐·배 탑승 상태)";
        }

        private string DescribeSuccess()
        {
            switch (_pendingAction)
            {
                case InteractionAction.SelectRecipe:
                    return "레시피를 선택했습니다.";
                case InteractionAction.Craft:
                    return $"{_pendingRecipeName} 제작 완료! 작업대 보관함에 넣었습니다.";
                default:
                    return "자원을 넣었습니다.";
            }
        }

        private void HandleWorkbenchChanged()
        {
            Refresh();
        }

        private void HandleResourceCountChanged(int resourceId, int count)
        {
            Refresh();
        }

        private void SetMessage(string text, Color color)
        {
            _message.text = text;
            _message.color = color;
        }

        private bool HasAnyDeposit(ModuleRecipe recipe)
        {
            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                if (_workbench.GetDepositedAt(i) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private int Held(int resourceId)
        {
            return _inventory ? _inventory.GetSyncedCount(resourceId) : 0;
        }

        private Sprite GetResourceIcon(ResourceDefinition resource)
        {
            if (!resource)
            {
                return null;
            }

            if (resource.Icon)
            {
                return resource.Icon;
            }

            for (int i = 0; i < _resourceIcons.Length; i++)
            {
                if (_resourceIcons[i].Resource == resource)
                {
                    return _resourceIcons[i].Icon;
                }
            }

            return null;
        }

        private string DescribeIngredients(ModuleRecipe recipe)
        {
            _builder.Clear();

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);

                if (i > 0)
                {
                    _builder.Append(" · ");
                }

                _builder.Append(ingredient.Resource ? ingredient.Resource.DisplayName : "?").Append(' ').Append(ingredient.Amount);
            }

            if (recipe.IsTestValue)
            {
                _builder.Append("  (시험값)");
            }

            return _builder.ToString();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current)
            {
                return;
            }

            GameObject eventSystem = new GameObject(nameof(EventSystem), typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
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
