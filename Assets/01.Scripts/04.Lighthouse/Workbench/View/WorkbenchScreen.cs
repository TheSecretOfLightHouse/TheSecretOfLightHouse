using System;
using System.Collections.Generic;
using System.Text;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Session.View;
using Lighthouse.Stations.View;
using Lighthouse.Tower.Rules;
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
    public sealed class WorkbenchScreen : MonoBehaviour, IStationScreen
    {
        private const string IdleMessage = "레시피를 골라 진행도를 보고 자원을 넣으세요. 넣은 자원은 되돌릴 수 없습니다.";

        [Serializable]
        private struct ResourceIcon
        {
            public ResourceDefinition Resource;
            public Sprite Icon;
        }

        [Serializable]
        private struct RecipeIcon
        {
            public ModuleRecipe Recipe;
            public Sprite Icon;
        }

        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private InteractionTarget _target;
        [SerializeField]
        private Key _openKey = Key.E;

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
        [SerializeField]
        private RecipeIcon[] _recipeIcons = new RecipeIcon[0];

        [Header("Progress")]
        [SerializeField]
        private Text _progressTitle;
        [SerializeField]
        private GameObject _progressEmpty;
        [SerializeField]
        private WorkbenchIngredientRow _rowTemplate;
        [SerializeField, Tooltip("Optional.")]
        private Text _progressHint;
        [SerializeField]
        private Button _craftButton;

        [Header("Sketch layout (optional)")]
        [SerializeField]
        private bool _isSketchLayout;
        [SerializeField]
        private Button _depositButton;
        [SerializeField]
        private RectTransform _overallFill;
        [SerializeField]
        private Button _takeButton;
        [SerializeField]
        private Button _previousStockPageButton;
        [SerializeField]
        private Button _nextStockPageButton;
        [SerializeField]
        private Text _stockPageText;

        [Header("Stock")]
        [SerializeField]
        private WorkbenchItemSlot[] _resourceSlots = new WorkbenchItemSlot[0];
        [SerializeField]
        private WorkbenchItemSlot[] _storedSlots = new WorkbenchItemSlot[0];
        [SerializeField]
        private ResourceIcon[] _resourceIcons = new ResourceIcon[0];

        [Header("Carry (2026-10-20): take a stored module, put it back")]
        [SerializeField]
        private GameObject _carryBar;
        [SerializeField]
        private Image _carryIcon;
        [SerializeField]
        private Text _carryText;
        [SerializeField]
        private Button _returnButton;
        [SerializeField]
        private Button _returnShortcutButton;

        [Header("Recipe sections")]
        [Tooltip("Inactive heading in the recipe list, copied once for lighthouse and once for boat modules.")]
        [SerializeField]
        private Text _sectionHeaderTemplate;
        [Tooltip("Optional. Used instead of the plain text heading when set (a plate with an icon).")]
        [SerializeField]
        private WorkbenchSectionHeader _sectionHeaderView;
        [SerializeField]
        private Sprite _lighthouseSectionIcon;
        [SerializeField]
        private Sprite _boatSectionIcon;

        [Header("Optional progress header pieces")]
        [SerializeField]
        private Image _progressIcon;
        [Tooltip("Shows where the module goes. Without it boat modules get \"(배 모듈)\" after the title.")]
        [SerializeField]
        private Text _progressKind;

        [Header("Badge colors (only when the badge sprite is tinted)")]
        [SerializeField]
        private bool _isBadgeTinted;
        [SerializeField]
        private Color _badgeReadyColor = new Color(0.27f, 0.5f, 0.34f);
        [SerializeField]
        private Color _badgeProgressColor = new Color(0.78f, 0.45f, 0.18f);
        [SerializeField]
        private Color _badgeCappedColor = new Color(0.7f, 0.27f, 0.2f);
        [Tooltip("Optional. Tells which floors each lighthouse recipe goes to, shown on its card.")]
        [SerializeField]
        private TowerLayout _towerLayout;

        [Header("Font")]
        [SerializeField]
        private bool _useOsFont = true;
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };

        private readonly List<WorkbenchRecipeCard> _cards = new List<WorkbenchRecipeCard>();
        private readonly List<WorkbenchIngredientRow> _rows = new List<WorkbenchIngredientRow>();
        private readonly StringBuilder _builder = new StringBuilder();
        private GameObject _lighthouseHeader;
        private GameObject _boatHeader;
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
        private int _selectedResourceId;
        private int _selectedStoredRecipeId;
        private int _stockPage;
        private int _stockPageCount = 1;
        // E acts on the last selected pane; choosing a material never takes a shelf module by accident.
        private bool _isStockFocused;

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

            if (_returnButton)
            {
                _returnButton.onClick.AddListener(HandleReturnClicked);
            }

            if (_returnShortcutButton)
            {
                _returnShortcutButton.onClick.AddListener(HandleReturnClicked);
            }

            if (_isSketchLayout)
            {
                if (_depositButton)
                {
                    _depositButton.onClick.AddListener(HandleSelectedDepositClicked);
                }

                if (_takeButton)
                {
                    _takeButton.onClick.AddListener(HandleSelectedTakeClicked);
                }

                if (_previousStockPageButton)
                {
                    _previousStockPageButton.onClick.AddListener(HandlePreviousStockPageClicked);
                }

                if (_nextStockPageButton)
                {
                    _nextStockPageButton.onClick.AddListener(HandleNextStockPageClicked);
                }
            }

            if (_sectionHeaderTemplate)
            {
                _sectionHeaderTemplate.gameObject.SetActive(false);
            }

            if (_sectionHeaderView)
            {
                _sectionHeaderView.gameObject.SetActive(false);
            }

            _window.SetActive(false);
            _prompt.SetActive(false);
        }

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.Changed += HandleWorkbenchChanged;
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

            bool isFocused = _isInRange && StationScreenFocus.IsNearest(this);
            _prompt.SetActive(isFocused && !_isOpen && !SessionScreen.IsGameplayOverlayOpen);
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (_isOpen && (keyboard.escapeKey.wasPressedThisFrame || !_isSketchLayout && keyboard[_openKey].wasPressedThisFrame))
            {
                Close();
            }
            else if (_isOpen && _isSketchLayout)
            {
                HandleSketchInput(keyboard);
            }
            else if (!_isOpen && isFocused && keyboard[_openKey].wasPressedThisFrame && !SessionScreen.IsGameplayOverlayOpen)
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

            StationScreenFocus.Unregister(this);
            Close();
            BindLocalPlayer(null);
        }

        public void Bind(WorkbenchNet workbench, InteractionTarget target)
        {
            Close();

            if (isActiveAndEnabled && _workbench)
            {
                _workbench.Changed -= HandleWorkbenchChanged;
            }

            _workbench = workbench;
            _target = target;
            _viewedRecipe = null;
            _isInRange = false;
            _prompt.SetActive(false);

            if (isActiveAndEnabled && _workbench)
            {
                _workbench.Changed += HandleWorkbenchChanged;
            }
        }

        public void Open()
        {
            if (_isOpen || !_workbench || !_workbench.Catalog || !_relay)
            {
                return;
            }

            _isOpen = true;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            EnsureEventSystem();
            BuildCards();
            PickDefaultRecipe();
            _selectedResourceId = 0;
            _isStockFocused = false;
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
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _window.SetActive(false);
            LockPlayer(false);
        }

        private void HandleSketchInput(Keyboard keyboard)
        {
            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (_isStockFocused)
                {
                    HandleSelectedTakeClicked();
                }
                else
                {
                    HandleSelectedDepositClicked();
                }
            }
            else if (keyboard.fKey.wasPressedThisFrame && _craftButton.interactable)
            {
                HandleCraftClicked();
            }
            else if (keyboard.gKey.wasPressedThisFrame)
            {
                HandleReturnClicked();
            }
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

        public bool TryGetLocalSqrDistance(out float sqrDistance)
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            sqrDistance = 0f;

            if (!player || !_target || !_workbench || !_workbench.isActiveAndEnabled)
            {
                return false;
            }

            float range = _target.Range;
            sqrDistance = (player.transform.position - _target.Position).sqrMagnitude;
            return sqrDistance <= range * range;
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
                    _cards[i].Bind(recipe, DescribeCard(recipe), _recipeFallbackIcon, HandleRecipeClicked, GetRecipeIcon(recipe));
                }
            }

            ArrangeSections(catalog);
        }

        // Lighthouse recipes under one heading, boat recipes under another, whatever the catalog order.
        private void ArrangeSections(ModuleRecipeCatalog catalog)
        {
            bool hasBoat = false;
            bool hasLighthouse = false;

            for (int i = 0; i < catalog.Count; i++)
            {
                hasBoat |= catalog[i].IsBoatModule;
                hasLighthouse |= !catalog[i].IsBoatModule;
            }

            if ((_sectionHeaderView || _sectionHeaderTemplate) && !_lighthouseHeader)
            {
                _lighthouseHeader = CreateSectionHeader("등대 모듈", _lighthouseSectionIcon);
                _boatHeader = CreateSectionHeader("배 모듈", _boatSectionIcon);
            }

            PlaceSection(_lighthouseHeader, hasLighthouse, false);
            PlaceSection(_boatHeader, hasBoat, true);
        }

        private GameObject CreateSectionHeader(string title, Sprite icon)
        {
            Transform list = _cardTemplate.transform.parent;

            if (_sectionHeaderView)
            {
                WorkbenchSectionHeader view = Instantiate(_sectionHeaderView, list);
                view.Bind(title, icon);
                return view.gameObject;
            }

            Text header = Instantiate(_sectionHeaderTemplate, list);
            header.text = title;
            return header.gameObject;
        }

        private void PlaceSection(GameObject header, bool isShown, bool isBoat)
        {
            if (header)
            {
                header.SetActive(isShown);
                header.transform.SetAsLastSibling();
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].gameObject.activeSelf && _cards[i].Recipe && _cards[i].Recipe.IsBoatModule == isBoat)
                {
                    _cards[i].transform.SetAsLastSibling();
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

                if (_isBadgeTinted)
                {
                    card.SetBadgeColor(_workbench.IsReadyToCraft(recipe.Id) ? _badgeReadyColor
                        : _workbench.IsStarted(recipe.Id) ? _badgeProgressColor
                        : _badgeCappedColor);
                }
            }
        }

        private void RefreshProgress(ModuleRecipe recipe)
        {
            _progressEmpty.SetActive(!recipe);
            RefreshProgressHeader(recipe);
            bool isReady = recipe && _workbench.IsReadyToCraft(recipe.Id);
            bool isCapped = recipe && IsCapped(recipe);
            // The sketch layout has no hint line; the other prefabs still show it.
            if (_progressHint)
            {
                _progressHint.gameObject.SetActive(recipe);
                _progressHint.text = isReady ? "재료가 모두 모였습니다. 제작을 누르면 완성됩니다."
                    : isCapped ? "보유 한도에 닿아 지금은 새로 만들 수 없습니다."
                    : recipe && _workbench.IsStarted(recipe.Id) ? "선원들이 함께 채우는 중입니다. 다른 레시피도 따로 채울 수 있습니다."
                    : "자원을 넣으면 제작이 시작됩니다.";
            }
            _craftButton.gameObject.SetActive(recipe);
            _craftButton.interactable = isReady;
            int count = recipe ? recipe.IngredientCount : 0;
            int depositedTotal = 0;
            int requiredTotal = 0;

            if (_isSketchLayout)
            {
                PickDefaultIngredient(recipe);
            }

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
                int deposited = _workbench.GetDepositedAt(recipe.Id, i);
                _rows[i].Bind(ingredient.ResourceId, resource ? resource.DisplayName : "?", GetResourceIcon(resource),
                    HandleDepositClicked, _isSketchLayout ? HandleIngredientSelected : null);
                _rows[i].SetProgress(deposited, ingredient.Amount, Held(ingredient.ResourceId), !isCapped);
                _rows[i].SetSelected(_isSketchLayout && !_isStockFocused && ingredient.ResourceId == _selectedResourceId);
                depositedTotal += deposited;
                requiredTotal += ingredient.Amount;
            }

            if (_isSketchLayout && _overallFill)
            {
                float ratio = requiredTotal > 0 ? Mathf.Clamp01((float)depositedTotal / requiredTotal) : 0f;
                _overallFill.anchorMax = new Vector2(ratio, 1f);
                _overallFill.gameObject.SetActive(ratio > 0f);
            }

            if (_isSketchLayout && _depositButton)
            {
                _depositButton.interactable = CanDepositSelected();
            }
        }

        private void PickDefaultIngredient(ModuleRecipe recipe)
        {
            if (!recipe || recipe.IndexOf(_selectedResourceId) >= 0)
            {
                return;
            }

            _selectedResourceId = recipe.IngredientCount > 0 ? recipe.GetIngredient(0).ResourceId : 0;

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                int resourceId = recipe.GetIngredient(i).ResourceId;

                if (Held(resourceId) > 0 && _workbench.GetRemaining(recipe.Id, resourceId) > 0)
                {
                    _selectedResourceId = resourceId;
                    return;
                }
            }
        }

        private bool CanDepositSelected()
        {
            return _viewedRecipe && !IsCapped(_viewedRecipe) && _viewedRecipe.IndexOf(_selectedResourceId) >= 0
                && Held(_selectedResourceId) > 0 && _workbench.GetRemaining(_viewedRecipe.Id, _selectedResourceId) > 0;
        }

        private void RefreshProgressHeader(ModuleRecipe recipe)
        {
            if (!_progressKind)
            {
                _progressTitle.text = !recipe ? "레시피를 고르세요" : recipe.IsBoatModule ? $"{recipe.DisplayName}  (배 모듈)" : recipe.DisplayName;
            }
            else
            {
                _progressTitle.text = recipe ? recipe.DisplayName : "레시피를 고르세요";
                _builder.Clear();

                if (recipe)
                {
                    AppendPlacement(recipe);
                }

                _progressKind.text = _builder.ToString();
            }

            if (_progressIcon)
            {
                _progressIcon.sprite = recipe ? GetRecipeIcon(recipe) : null;
                _progressIcon.enabled = _progressIcon.sprite;
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

            if (_isSketchLayout)
            {
                RefreshSketchStock(catalog);
                RefreshCarry(catalog);
                return;
            }

            // One module at a time (2026-10-20): with one in hand every take button is off until it is put back.
            bool canTake = _workbench.OwnCarriedRecipe == 0;
            int slot = 0;

            for (int i = 0; i < catalog.Count && slot < _storedSlots.Length; i++)
            {
                int stored = _workbench.GetStoredCount(catalog[i].Id);

                if (stored > 0)
                {
                    ModuleRecipe recipe = catalog[i];
                    _storedSlots[slot].Show(GetRecipeIcon(recipe), recipe.DisplayName, stored);
                    _storedSlots[slot++].SetAction(recipe.Id, canTake, HandleTakeClicked);
                }
            }

            for (; slot < _storedSlots.Length; slot++)
            {
                _storedSlots[slot].Clear();
            }

            RefreshCarry(catalog);
        }

        private void RefreshSketchStock(ModuleRecipeCatalog catalog)
        {
            int storedKinds = 0;

            for (int i = 0; i < catalog.Count; i++)
            {
                if (_workbench.GetStoredCount(catalog[i].Id) > 0)
                {
                    storedKinds++;
                }
            }

            int capacity = _storedSlots.Length;
            _stockPageCount = capacity > 0 ? Mathf.Max(1, (storedKinds + capacity - 1) / capacity) : 1;
            _stockPage = Mathf.Clamp(_stockPage, 0, _stockPageCount - 1);
            int first = _stockPage * capacity;
            int storedIndex = 0;
            int slot = 0;
            bool isSelectionVisible = false;

            for (int i = 0; i < catalog.Count && slot < capacity; i++)
            {
                ModuleRecipe recipe = catalog[i];
                int stored = _workbench.GetStoredCount(recipe.Id);

                if (stored <= 0)
                {
                    continue;
                }

                if (storedIndex++ < first)
                {
                    continue;
                }

                bool isSelected = recipe.Id == _selectedStoredRecipeId;
                isSelectionVisible |= isSelected;
                _storedSlots[slot].Show(GetRecipeIcon(recipe), recipe.DisplayName, stored);
                _storedSlots[slot].SetAction(recipe.Id, true, HandleStoredSelected);
                _storedSlots[slot].SetSelected(isSelected && _isStockFocused);
                slot++;
            }

            for (; slot < capacity; slot++)
            {
                _storedSlots[slot].Clear();
            }

            if (!isSelectionVisible)
            {
                _selectedStoredRecipeId = 0;
            }

            if (_takeButton)
            {
                _takeButton.interactable = _selectedStoredRecipeId != 0 && _workbench.OwnCarriedRecipe == 0;
            }

            if (_previousStockPageButton)
            {
                _previousStockPageButton.interactable = _stockPage > 0;
            }

            if (_nextStockPageButton)
            {
                _nextStockPageButton.interactable = _stockPage + 1 < _stockPageCount;
            }

            if (_stockPageText)
            {
                _stockPageText.text = $"{_stockPage + 1} / {_stockPageCount}";
            }
        }

        // Only this player's own module: who holds what is private (2026-10-21).
        private void RefreshCarry(ModuleRecipeCatalog catalog)
        {
            if (!_carryBar)
            {
                return;
            }

            int carriedId = _workbench.OwnCarriedRecipe;
            ModuleRecipe carried = carriedId != 0 && catalog.TryGetById(carriedId, out ModuleRecipe recipe) ? recipe : null;
            _carryText.text = carried ? $"인벤토리 모듈: {carried.DisplayName}" : "인벤토리 모듈: 없음";

            if (_carryIcon)
            {
                _carryIcon.sprite = carried ? GetRecipeIcon(carried) : null;
                _carryIcon.enabled = _carryIcon.sprite;
            }

            if (_returnButton)
            {
                _returnButton.gameObject.SetActive(_isSketchLayout || carried);
                _returnButton.interactable = carried;
            }

            if (_returnShortcutButton)
            {
                _returnShortcutButton.interactable = carried;
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
            _selectedResourceId = 0;
            _isStockFocused = false;
            SetMessage(IdleMessage, _messageInfoColor);
            Refresh();
        }

        private void HandleIngredientSelected(int resourceId)
        {
            _selectedResourceId = resourceId;
            _isStockFocused = false;
            Refresh();
        }

        private void HandleStoredSelected(int recipeId)
        {
            _selectedStoredRecipeId = recipeId;
            _isStockFocused = true;
            Refresh();
        }

        private void HandleSelectedDepositClicked()
        {
            if (CanDepositSelected())
            {
                _isStockFocused = false;
                HandleDepositClicked(_selectedResourceId, false);
                Refresh();
            }
        }

        private void HandleSelectedTakeClicked()
        {
            if (_selectedStoredRecipeId != 0 && _workbench.OwnCarriedRecipe == 0
                && _workbench.GetStoredCount(_selectedStoredRecipeId) > 0)
            {
                HandleTakeClicked(_selectedStoredRecipeId);
            }
        }

        private void HandlePreviousStockPageClicked()
        {
            if (_stockPage > 0)
            {
                _stockPage--;
                _selectedStoredRecipeId = 0;
                RefreshStock();
            }
        }

        private void HandleNextStockPageClicked()
        {
            if (_stockPage + 1 < _stockPageCount)
            {
                _stockPage++;
                _selectedStoredRecipeId = 0;
                RefreshStock();
            }
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

        private void HandleTakeClicked(int recipeId)
        {
            if (_workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe) && Send(InteractionAction.TakeModule, recipeId, 0, 0))
            {
                _pendingRecipeName = recipe.DisplayName;
            }
        }

        private void HandleReturnClicked()
        {
            int carriedId = _workbench.OwnCarriedRecipe;

            if (carriedId != 0 && _workbench.Catalog.TryGetById(carriedId, out ModuleRecipe recipe) && Send(InteractionAction.ReturnModule, 0, 0, 0))
            {
                _pendingRecipeName = recipe.DisplayName;
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

            switch (_pendingAction)
            {
                case InteractionAction.Craft:
                    return "제작할 수 없습니다. (재료가 모자라거나 다른 선원이 먼저 제작함, 쓰러짐·배 탑승 상태)";
                case InteractionAction.TakeModule:
                    return $"{_pendingRecipeName}을(를) 가져갈 수 없습니다. (이미 모듈을 들고 있거나 다른 선원이 먼저 가져감, 쓰러짐·배 탑승 상태)";
                case InteractionAction.ReturnModule:
                    return "되돌려놓을 수 없습니다. (이 작업대에서 가져온 모듈만, 쓰러짐·배 탑승 상태)";
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
                case InteractionAction.TakeModule:
                    return $"{_pendingRecipeName}을(를) 가져갔습니다. 등대나 내 배에서 장착하세요.";
                case InteractionAction.ReturnModule:
                    return $"{_pendingRecipeName}을(를) 보관함에 되돌려놓았습니다.";
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
            // The sketch layout keeps the line empty until a request answers.
            _message.text = _isSketchLayout && text == IdleMessage ? string.Empty : text;
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

            if (resource.Icon && !_isSketchLayout)
            {
                return resource.Icon;
            }

            for (int i = 0; i < _resourceIcons.Length; i++)
            {
                if (_resourceIcons[i].Resource == resource && _resourceIcons[i].Icon)
                {
                    return _resourceIcons[i].Icon;
                }
            }

            return resource.Icon;
        }

        private Sprite GetRecipeIcon(ModuleRecipe recipe)
        {
            for (int i = 0; i < _recipeIcons.Length; i++)
            {
                if (_recipeIcons[i].Recipe == recipe && _recipeIcons[i].Icon)
                {
                    return _recipeIcons[i].Icon;
                }
            }

            return recipe.Icon ? recipe.Icon : _recipeFallbackIcon;
        }

        // Ingredients on the first line, where the module goes on the second.
        private string DescribeCard(ModuleRecipe recipe)
        {
            _builder.Clear();
            AppendIngredients(recipe);
            _builder.Append('\n');
            AppendPlacement(recipe);

            if (recipe.IsTestValue)
            {
                _builder.Append("  (시험값)");
            }

            return _builder.ToString();
        }

        // "배 모듈", or the lighthouse floors from the layout, such as "등대 1층~3층". Nothing is added without a layout.
        private void AppendPlacement(ModuleRecipe recipe)
        {
            if (recipe.IsBoatModule)
            {
                _builder.Append("배 모듈");
                return;
            }

            _builder.Append("등대");

            if (!_towerLayout)
            {
                return;
            }

            int lowest = -1;
            int highest = -1;

            for (int i = 0; i < _towerLayout.SlotCount; i++)
            {
                TowerSlot slot = _towerLayout[i];

                if (slot.RecipeId != recipe.Id)
                {
                    continue;
                }

                lowest = lowest < 0 ? slot.Floor : Mathf.Min(lowest, slot.Floor);
                highest = Mathf.Max(highest, slot.Floor);
            }

            if (lowest < 0)
            {
                return;
            }

            _builder.Append(' ').Append(_towerLayout.GetFloorName(lowest));

            if (highest != lowest)
            {
                _builder.Append('~').Append(_towerLayout.GetFloorName(highest));
            }
        }

        private void AppendIngredients(ModuleRecipe recipe)
        {
            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);

                if (i > 0)
                {
                    _builder.Append(" · ");
                }

                _builder.Append(ingredient.Resource ? ingredient.Resource.DisplayName : "?").Append(' ').Append(ingredient.Amount);
            }
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
