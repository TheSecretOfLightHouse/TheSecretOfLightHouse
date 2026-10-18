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
    // Canvas screen for the shared workbench (JH03; the workbench UI moved to JH03 on 2026-10-18). Opens with the open key
    // while the local player is inside the workbench's interaction range, and sends the same relay requests as
    // real input would. Which recipe is shown is this player's own choice and never leaves this machine; the
    // progress of every recipe is shared (2026-10-18). Everything shown comes from WorkbenchNet's public state
    // and the local inventory.
    [DisallowMultipleComponent]
    public sealed class WorkbenchScreen : MonoBehaviour
    {
        private const string IdleMessage = "레시피를 골라 진행도를 보고 자원을 넣으세요. 넣은 자원은 되돌릴 수 없습니다.";

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
        // Local view only: other players may be looking at a different recipe.
        private ModuleRecipe _viewedRecipe;
        private InteractionAction _pendingAction;
        private int _pendingResourceId;
        private string _pendingRecipeName;

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
            _window.SetActive(false);
            _prompt.SetActive(false);
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

            if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || keyboard[_openKey].wasPressedThisFrame))
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
            PickDefaultRecipe();
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
        // screen is open. A proper input lock belongs to the player side; this stands in until it exists.
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

        // Keeps the last recipe this player looked at; otherwise one already in progress, otherwise the first.
        private void PickDefaultRecipe()
        {
            ModuleRecipeCatalog catalog = _workbench.Catalog;

            if (_viewedRecipe || catalog.Count == 0)
            {
                return;
            }

            _viewedRecipe = catalog[0];

            for (int i = 0; i < catalog.Count; i++)
            {
                if (_workbench.IsStarted(catalog[i].Id))
                {
                    _viewedRecipe = catalog[i];
                    return;
                }
            }
        }

        private void Refresh()
        {
            if (!_isOpen)
            {
                return;
            }

            RefreshCards();
            RefreshProgress(_viewedRecipe);
            RefreshStock();
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                WorkbenchRecipeCard card = _cards[i];
                ModuleRecipe recipe = card.Recipe;

                if (!card.gameObject.activeSelf || !recipe)
                {
                    continue;
                }

                bool isSelected = recipe == _viewedRecipe;
                card.SetState(isSelected, !isSelected, DescribeBadge(recipe));
            }
        }

        private void RefreshProgress(ModuleRecipe recipe)
        {
            _progressEmpty.SetActive(!recipe);
            _progressTitle.text = recipe ? recipe.DisplayName : "레시피를 고르세요";
            bool isReady = recipe && _workbench.IsReadyToCraft(recipe.Id);
            bool isCapped = recipe && IsCapped(recipe);
            _progressHint.gameObject.SetActive(recipe);
            _progressHint.text = isReady ? "재료가 모두 모였습니다. 제작을 누르면 완성됩니다."
                : isCapped ? "보유 한도에 닿아 지금은 새로 만들 수 없습니다."
                : recipe && _workbench.IsStarted(recipe.Id) ? "선원들이 함께 채우는 중입니다. 다른 레시피도 따로 채울 수 있습니다."
                : "자원을 넣으면 제작이 시작됩니다.";
            _craftButton.gameObject.SetActive(recipe);
            _craftButton.interactable = isReady;
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
                _rows[i].SetProgress(_workbench.GetDepositedAt(recipe.Id, i), ingredient.Amount, Held(ingredient.ResourceId), !isCapped);
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

        private bool Send(InteractionAction action, int arg0, int arg1, int arg2)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1, arg2))
            {
                SetMessage("이전 요청을 처리하는 중입니다. 잠시 후 다시 시도하세요.", _messageRejectColor);
                return false;
            }

            _pendingAction = action;
            return true;
        }

        // Switching recipes only changes what this player sees, so nothing is sent.
        private void HandleRecipeClicked(ModuleRecipe recipe)
        {
            _viewedRecipe = recipe;
            SetMessage(IdleMessage, _messageInfoColor);
            Refresh();
        }

        private void HandleDepositClicked(int resourceId, bool isAll)
        {
            if (!_viewedRecipe)
            {
                return;
            }

            int amount = isAll ? _workbench.GetRemaining(_viewedRecipe.Id, resourceId) : 1;

            if (Send(InteractionAction.Deposit, _viewedRecipe.Id, resourceId, Mathf.Max(1, amount)))
            {
                _pendingResourceId = resourceId;
                _pendingRecipeName = _viewedRecipe.DisplayName;
            }
        }

        private void HandleCraftClicked()
        {
            if (_viewedRecipe && Send(InteractionAction.Craft, _viewedRecipe.Id, 0, 0))
            {
                _pendingRecipeName = _viewedRecipe.DisplayName;
            }
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

            return Held(_pendingResourceId) <= 0
                ? "넣을 자원이 없습니다."
                : $"{_pendingRecipeName}에 넣을 수 없습니다. (다 찬 재료, 보유 한도 또는 쓰러짐·배 탑승 상태)";
        }

        private string DescribeSuccess()
        {
            switch (_pendingAction)
            {
                case InteractionAction.Craft:
                    return $"{_pendingRecipeName} 제작 완료! 작업대 보관함에 넣었습니다.";
                default:
                    return $"{_pendingRecipeName}에 자원을 넣었습니다.";
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

        // A recipe already in progress keeps its module, so the cap only stops starting a new one.
        // Counts stored modules only until installed and drifting modules are public (JH04·JH05).
        private bool IsCapped(ModuleRecipe recipe)
        {
            return recipe.HasExistenceLimit && !_workbench.IsStarted(recipe.Id) && _workbench.GetStoredCount(recipe.Id) >= recipe.MaxExisting;
        }

        private string DescribeBadge(ModuleRecipe recipe)
        {
            if (_workbench.IsReadyToCraft(recipe.Id))
            {
                return "제작 가능";
            }

            if (_workbench.IsStarted(recipe.Id))
            {
                return "진행 중";
            }

            return IsCapped(recipe) ? "보유 한도" : null;
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
