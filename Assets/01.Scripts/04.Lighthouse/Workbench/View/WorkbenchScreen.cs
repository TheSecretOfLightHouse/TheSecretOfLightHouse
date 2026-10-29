using System;
using System.Collections.Generic;
using System.Text;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
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

        private static int _openCount;

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
        [Tooltip("Mafia only (2026-10-27): takes one of the selected ingredient back out. Shown to the mafia alone; the server checks the role.")]
        [SerializeField]
        private Button _withdrawButton;
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
        [Tooltip("Shown with the screen open, and with it closed while a module is carried (2026-10-27). Keep it outside the window for that.")]
        [SerializeField]
        private GameObject _carryBar;
        [SerializeField]
        private Image _carryIcon;
        [SerializeField]
        private Text _carryText;
        [SerializeField]
        private Button _returnButton;

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

        [Header("Effects (2026-10-27): deposit flight, ready glow, other crew's deposits, rejection")]
        [SerializeField]
        private WorkbenchScreenEffects.Settings _effectSettings = new WorkbenchScreenEffects.Settings();

        private readonly List<WorkbenchRecipeCard> _cards = new List<WorkbenchRecipeCard>();
        private readonly List<WorkbenchIngredientRow> _rows = new List<WorkbenchIngredientRow>();
        private readonly List<bool> _wasCardReady = new List<bool>();
        // Deposited amounts of the viewed recipe as last drawn, to tell what came in since.
        private readonly List<int> _seenDeposited = new List<int>();
        private WorkbenchScreenEffects _effects;
        private bool _hasSeenCards;
        private int _seenRecipeId;
        private float _overallShown;
        private float _overallTarget;
        // Who deposited is never sent (contributors are server-only), and the result can reach this machine before
        // or after the new amount. So this player's own deposit is remembered here and taken out of the next
        // increase on that ingredient; whatever is left came from someone else.
        private int _ownCreditRecipeId;
        private int _ownCreditIndex;
        private int _ownCredit;
        private float _ownCreditUntil;
        private bool _isDepositAwaiting;
        private int _pendingRecipeId;
        // Who crafted is not sent either, and this player's result and the finish call (sent to everyone) may arrive in
        // either order. A finish for the recipe this player asked to craft waits for the result: a success makes it
        // this player's own, a failure means someone else got there first. A success that comes first leaves one
        // finish of that recipe still to be taken as its own.
        private int _craftAwaitingRecipeId;
        private int _heldFinishRecipeId;
        private int _heldFinishBaseline;
        // Last finish seen on this machine, to tell a craft's drop from a withdraw.
        private int _lastFinishRecipeId;
        private float _lastFinishTime = float.NegativeInfinity;
        // Rows of the viewed recipe that dropped (bit per ingredient) waiting to be told apart: a craft empties them
        // too, and on the host the finish call comes just after the drop.
        private int _dropRecipeId;
        private int _dropRows;
        private float _dropSeenAt;
        private int _ownFinishRecipeId;
        private float _ownFinishUntil;
        // The module this player crafts stays out of their stock until the show lands it there (2026-10-27): one above the
        // count at the click is left off, and while the icon flies its cell waits as an empty frame. Only this screen;
        // the server holds the module as stored all along.
        private int _reservedRecipeId;
        private int _reservedBaseline;
        private bool _isReservedLanding;
        // This player's craft playing out: gather, burst, flight to the stock, landing.
        private int _craftShowRecipeId;
        private Sprite _craftShowIcon;
        private int _craftShowStage;
        private float _craftShowElapsed;
        // Someone else's finished module, pulsed on its stock cell once the new count is in.
        private int _stockPulseRecipeId;
        private int _stockPulseBaseline;
        private float _stockPulseUntil;
        // Take and put back, shown on this player's screen only (what one carries is private): the module flies between
        // its stock cell and the carry bar, and shows up where it lands only once it lands. From the click (the host
        // hears of the change before the result) a taken module is kept off the carry bar, and a put-back one off the
        // stock, by one above the count at the click. The start is taken on the click too, as cell or bar may change.
        private int _carryMoveRecipeId;
        private bool _isCarryMoveTake;
        private Vector3 _carryMoveFrom;
        private bool _isCarryHeld;
        private int _returnReservedRecipeId;
        private int _returnReservedBaseline;
        private bool _isReturnLanding;
        private int _carryRevealRecipeId;
        private float _carryRevealAt;
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
        // True while this machine has any workbench screen open, for effects elsewhere that the screen already shows.
        public static bool IsAnyOpen => _openCount > 0;

        // Sound hook; nothing listens yet.
        public event Action<WorkbenchEffectCue> EffectCued;

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

            if (_isSketchLayout)
            {
                if (_depositButton)
                {
                    _depositButton.onClick.AddListener(HandleSelectedDepositClicked);
                }

                if (_withdrawButton)
                {
                    _withdrawButton.onClick.AddListener(HandleSelectedWithdrawClicked);
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

            _effects = new WorkbenchScreenEffects(_effectSettings, CreateEffectLayer(), _message.font, _craftButton, HandleEffectCued);
            _window.SetActive(false);
            _prompt.SetActive(false);
        }

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.Changed += HandleWorkbenchChanged;
                _workbench.RecipeCompleted += HandleRecipeCompleted;
            }

            StationScreenFocus.Register(this);
            RefreshCarryCard();
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
                TickEffects(Time.unscaledDeltaTime);
            }
            // Other screens open and close without telling the workbench, so the card follows them here.
            else if (_carryBar && _workbench && _carryBar.activeSelf != IsCarryCardShown())
            {
                RefreshCarryCard();
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
                _workbench.RecipeCompleted -= HandleRecipeCompleted;
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
            _openCount++;
            SessionScreen.SetGameplayOverlayOpen(this, true);
            EnsureEventSystem();
            BuildCards();
            PickDefaultRecipe();
            _selectedResourceId = 0;
            _isStockFocused = false;
            // The first draw after opening only takes note; nothing that happened while closed plays.
            _hasSeenCards = false;
            _seenRecipeId = 0;
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
            _openCount--;
            SessionScreen.SetGameplayOverlayOpen(this, false);
            _effects.StopAll();
            _ownCredit = 0;
            _craftShowRecipeId = 0;
            _stockPulseRecipeId = 0;
            ReleaseReserved();
            ReleaseCarryHolds();
            _dropRows = 0;
            _window.SetActive(false);
            RefreshCarryCard();
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
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                HandleSelectedWithdrawClicked();
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
            RefreshCarryCard();

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
            while (_wasCardReady.Count < _cards.Count)
            {
                _wasCardReady.Add(false);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                WorkbenchRecipeCard card = _cards[i];
                ModuleRecipe recipe = card.Recipe;

                if (!card.gameObject.activeSelf || !recipe)
                {
                    continue;
                }

                bool isSelected = recipe == _viewedRecipe;
                bool isReady = _workbench.IsReadyToCraft(recipe.Id);
                card.SetState(isSelected, !isSelected, DescribeBadge(recipe));

                if (_isBadgeTinted)
                {
                    card.SetBadgeColor(isReady ? _badgeReadyColor
                        : _workbench.IsStarted(recipe.Id) ? _badgeProgressColor
                        : _badgeCappedColor);
                }

                if (_hasSeenCards && isReady && !_wasCardReady[i])
                {
                    _effects.PlayPunch(card.BadgeRect, _effectSettings.BadgePunchStrength);
                    HandleEffectCued(WorkbenchEffectCue.ReadyToCraft);
                }

                _wasCardReady[i] = isReady;
            }

            _hasSeenCards = true;
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
            _effects.SetReadyGlow(isReady);
            int count = recipe ? recipe.IngredientCount : 0;
            int depositedTotal = 0;
            int requiredTotal = 0;
            // A recipe just switched to (or the screen just opened) is drawn as is, without effects.
            bool isTracked = recipe && recipe.Id == _seenRecipeId;

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

                if (!isTracked)
                {
                    _rows[i].SnapFill();
                }
                else if (i < _seenDeposited.Count && deposited > _seenDeposited[i])
                {
                    HandleDepositSeen(recipe.Id, i, deposited - _seenDeposited[i]);
                }
                else if (i < _seenDeposited.Count && deposited < _seenDeposited[i])
                {
                    QueueDropFlash(recipe.Id, i);
                }
            }

            _seenDeposited.Clear();

            for (int i = 0; i < count; i++)
            {
                _seenDeposited.Add(_workbench.GetDepositedAt(recipe.Id, i));
            }

            _seenRecipeId = recipe ? recipe.Id : 0;

            if (_isSketchLayout && _overallFill)
            {
                _overallTarget = requiredTotal > 0 ? Mathf.Clamp01((float)depositedTotal / requiredTotal) : 0f;

                if (!isTracked || _overallTarget < _overallShown)
                {
                    _overallShown = _overallTarget;
                }

                ApplyOverallFill();
            }

            if (_isSketchLayout && _depositButton)
            {
                _depositButton.interactable = CanDepositSelected();
            }

            if (_isSketchLayout && _withdrawButton)
            {
                _withdrawButton.gameObject.SetActive(recipe && IsOwnMafia());
                _withdrawButton.interactable = CanWithdrawSelected();
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

        private bool CanWithdrawSelected()
        {
            return IsOwnMafia() && _viewedRecipe && _workbench.GetDeposited(_viewedRecipe.Id, _selectedResourceId) > 0;
        }

        // Only the player's own role is ever sent to a client (2026-10-22), so this hides the button from citizens; the
        // server still checks the role on every withdraw.
        private static bool IsOwnMafia()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            return manager && manager.OwnRole == MatchRole.Mafia;
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
                ModuleRecipe recipe = catalog[i];
                int stored = ShownStoredCount(recipe.Id);

                if (stored > 0)
                {
                    _storedSlots[slot].Show(GetRecipeIcon(recipe), recipe.DisplayName, stored);
                    _storedSlots[slot++].SetAction(recipe.Id, canTake, HandleTakeClicked);
                }
                else if (IsAwaitingCell(recipe.Id))
                {
                    _storedSlots[slot].ShowAwaiting();
                    _storedSlots[slot++].SetAction(recipe.Id, false, HandleTakeClicked);
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
                if (ShownStoredCount(catalog[i].Id) > 0 || IsAwaitingCell(catalog[i].Id))
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
                int stored = ShownStoredCount(recipe.Id);
                bool isAwaiting = stored <= 0 && IsAwaitingCell(recipe.Id);

                if (stored <= 0 && !isAwaiting)
                {
                    continue;
                }

                if (storedIndex++ < first)
                {
                    continue;
                }

                if (isAwaiting)
                {
                    _storedSlots[slot].ShowAwaiting();
                    _storedSlots[slot].SetAction(recipe.Id, false, HandleStoredSelected);
                    _storedSlots[slot].SetSelected(false);
                    slot++;
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
        // Flights are drawn over everything on this canvas: the carried module card sits outside the window, after it,
        // and an icon taken into that card would otherwise pass behind it (2026-10-27).
        private RectTransform CreateEffectLayer()
        {
            GameObject layer = new GameObject("Effects", typeof(RectTransform));
            layer.layer = gameObject.layer;
            RectTransform rect = (RectTransform)layer.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return rect;
        }

        // With the screen closed the card stays only while a module is carried, where it was (2026-10-27). Another
        // screen (the lighthouse one shows the carried module itself) hides it, as it would cover that screen.
        private bool IsCarryCardShown()
        {
            return _isOpen || _workbench.OwnCarriedRecipe != 0 && !SessionScreen.IsGameplayOverlayOpen;
        }

        private void RefreshCarryCard()
        {
            if (!_carryBar || !_workbench || !_workbench.Catalog)
            {
                return;
            }

            bool isShown = IsCarryCardShown();
            _carryBar.SetActive(isShown);

            if (isShown && !_isOpen)
            {
                RefreshCarry(_workbench.Catalog);
            }
        }

        private void RefreshCarry(ModuleRecipeCatalog catalog)
        {
            if (!_carryBar)
            {
                return;
            }

            // A module being taken shows here only once its flight lands.
            int carriedId = _isCarryHeld ? 0 : _workbench.OwnCarriedRecipe;
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

        // One unit of the selected ingredient back into the mafia's inventory, through the existing Withdraw request.
        private void HandleSelectedWithdrawClicked()
        {
            if (!CanWithdrawSelected())
            {
                return;
            }

            _isStockFocused = false;

            if (Send(InteractionAction.Withdraw, _viewedRecipe.Id, _selectedResourceId, 1))
            {
                _pendingResourceId = _selectedResourceId;
                _pendingRecipeName = _viewedRecipe.DisplayName;
            }
        }

        private void HandleSelectedTakeClicked()
        {
            if (_selectedStoredRecipeId != 0 && _workbench.OwnCarriedRecipe == 0
                && ShownStoredCount(_selectedStoredRecipeId) > 0)
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

            int amount = Mathf.Max(1, isAll ? _workbench.GetRemaining(_viewedRecipe.Id, resourceId) : 1);

            if (Send(InteractionAction.Deposit, _viewedRecipe.Id, resourceId, amount))
            {
                _pendingResourceId = resourceId;
                _pendingRecipeId = _viewedRecipe.Id;
                _pendingRecipeName = _viewedRecipe.DisplayName;
                _isDepositAwaiting = true;
                // The server clamps to what is held and still missing, so this never exceeds the real deposit.
                _ownCreditRecipeId = _viewedRecipe.Id;
                _ownCreditIndex = _viewedRecipe.IndexOf(resourceId);
                _ownCredit = Mathf.Min(amount, Held(resourceId));
                _ownCreditUntil = float.PositiveInfinity;
            }
        }

        private void HandleCraftClicked()
        {
            if (_viewedRecipe && Send(InteractionAction.Craft, _viewedRecipe.Id, 0, 0))
            {
                _pendingRecipeName = _viewedRecipe.DisplayName;
                _craftAwaitingRecipeId = _viewedRecipe.Id;
                _heldFinishRecipeId = 0;
                // Taken on the click: the host's stock can count the new module before the result arrives.
                _reservedRecipeId = _viewedRecipe.Id;
                _reservedBaseline = _workbench.GetStoredCount(_viewedRecipe.Id);
                _isReservedLanding = false;
            }
        }

        private void HandleTakeClicked(int recipeId)
        {
            if (_workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe) && Send(InteractionAction.TakeModule, recipeId, 0, 0))
            {
                _pendingRecipeName = recipe.DisplayName;
                WorkbenchItemSlot slot = FindStoredSlot(recipeId);
                RectTransform from = slot ? slot.IconRect : _takeButton ? (RectTransform)_takeButton.transform : null;
                _carryMoveRecipeId = from && _carryIcon ? recipeId : 0;
                _isCarryMoveTake = true;
                _carryMoveFrom = from ? WorkbenchScreenEffects.CenterOf(from) : Vector3.zero;
                _isCarryHeld = _carryMoveRecipeId != 0;
            }
        }

        private void HandleReturnClicked()
        {
            // Not before a module being taken has landed in hand.
            int carriedId = _isCarryHeld ? 0 : _workbench.OwnCarriedRecipe;

            if (carriedId != 0 && _workbench.Catalog.TryGetById(carriedId, out ModuleRecipe recipe) && Send(InteractionAction.ReturnModule, 0, 0, 0))
            {
                _pendingRecipeName = recipe.DisplayName;
                _carryMoveRecipeId = _carryIcon ? carriedId : 0;
                _isCarryMoveTake = false;
                _carryMoveFrom = _carryIcon ? WorkbenchScreenEffects.CenterOf(_carryIcon.rectTransform) : Vector3.zero;

                if (_carryMoveRecipeId != 0)
                {
                    _returnReservedRecipeId = carriedId;
                    _returnReservedBaseline = _workbench.GetStoredCount(carriedId);
                    _isReturnLanding = false;
                }
            }
        }

        private void HandleResultReceived(InteractionResult result)
        {
            bool wasDeposit = _isDepositAwaiting;
            _isDepositAwaiting = false;
            int craftedId = _craftAwaitingRecipeId;
            bool isFinishHeld = craftedId != 0 && _heldFinishRecipeId == craftedId;
            _craftAwaitingRecipeId = 0;
            _heldFinishRecipeId = 0;
            int carryMoveId = _carryMoveRecipeId;
            _carryMoveRecipeId = 0;

            if (craftedId != 0)
            {
                if (result.IsSuccess && !isFinishHeld)
                {
                    _ownFinishRecipeId = craftedId;
                    _ownFinishUntil = Time.unscaledTime + _effectSettings.CraftWaitSeconds;
                }
                else if (!result.IsSuccess && isFinishHeld)
                {
                    // Pressed together and lost: the finish that came in was someone else's.
                    QueueStockPulse(craftedId, _heldFinishBaseline);
                }

                // Nothing will land without a show, so the module goes straight into the stock.
                if (!result.IsSuccess || !_isOpen)
                {
                    ReleaseReserved();
                }
            }

            if (carryMoveId != 0 && (!result.IsSuccess || !_isOpen))
            {
                ReleaseCarryHolds();
            }

            if (wasDeposit)
            {
                if (result.IsSuccess)
                {
                    // The new amount may still be on its way; give it a moment to arrive and be matched.
                    _ownCreditUntil = Time.unscaledTime + _effectSettings.OwnDepositSeconds;
                }
                else
                {
                    _ownCredit = 0;
                }
            }

            if (!_isOpen)
            {
                return;
            }

            if (result.IsSuccess)
            {
                SetMessage(DescribeSuccess(), _messageSuccessColor);

                if (wasDeposit)
                {
                    PlayDepositLanded();
                }
                else if (craftedId != 0)
                {
                    StartCraftShow(craftedId);
                }
                else if (carryMoveId != 0)
                {
                    PlayCarryMove(carryMoveId);
                }

                return;
            }

            SetMessage(DescribeRejection(result.Code), _messageRejectColor);

            if (wasDeposit)
            {
                PlayDepositRejected();
            }
        }

        // The icon flies from this player's stock (or the deposit button when no stock cell shows it) to the row.
        private void PlayDepositLanded()
        {
            if (!_viewedRecipe || _viewedRecipe.Id != _pendingRecipeId)
            {
                return;
            }

            WorkbenchIngredientRow row = FindRow(_pendingResourceId);

            if (!row)
            {
                return;
            }

            WorkbenchItemSlot slot = FindResourceSlot(_pendingResourceId);
            RectTransform from = slot ? slot.IconRect
                : _depositButton && _depositButton.gameObject.activeInHierarchy ? (RectTransform)_depositButton.transform
                : row.AddOneRect;

            if (slot)
            {
                _effects.PlayPunch(slot.CountRect, _effectSettings.PunchStrength);
            }

            if (from)
            {
                _effects.PlayFlight(row.Icon.sprite, from, row.Icon.rectTransform, row.CountRect);
            }
            else
            {
                _effects.PlayPunch(row.Icon.rectTransform, _effectSettings.PunchStrength);
                _effects.PlayPunch(row.CountRect, _effectSettings.PunchStrength);
                HandleEffectCued(WorkbenchEffectCue.DepositLanded);
            }
        }

        private void PlayDepositRejected()
        {
            WorkbenchIngredientRow row = _viewedRecipe && _viewedRecipe.Id == _pendingRecipeId ? FindRow(_pendingResourceId) : null;
            RectTransform button = _isSketchLayout && _depositButton ? (RectTransform)_depositButton.transform
                : row ? row.AddOneRect
                : null;
            _effects.PlayShake(button);
            _effects.PlayFlash(_message);

            if (row)
            {
                _effects.PlayFlash(row.Icon);
            }

            HandleEffectCued(WorkbenchEffectCue.DepositRejected);
        }

        private void HandleDepositSeen(int recipeId, int ingredientIndex, int added)
        {
            if (_ownCredit > 0 && _ownCreditRecipeId == recipeId && _ownCreditIndex == ingredientIndex)
            {
                int own = Mathf.Min(_ownCredit, added);
                _ownCredit -= own;
                added -= own;
            }

            if (added <= 0)
            {
                return;
            }

            WorkbenchIngredientRow row = _rows[ingredientIndex];
            _effects.PlayGain(row.CountRect, added);
            _effects.PlayPunch(row.CountRect, _effectSettings.PunchStrength);
            HandleEffectCued(WorkbenchEffectCue.OtherDeposited);
        }

        // Comes on every machine, open screen or not, so the bookkeeping for this player's own craft stays right.
        private void HandleRecipeCompleted(int recipeId)
        {
            _lastFinishRecipeId = recipeId;
            _lastFinishTime = Time.unscaledTime;

            if (recipeId == _craftAwaitingRecipeId)
            {
                _heldFinishRecipeId = recipeId;
                _heldFinishBaseline = StockPulseBaseline(recipeId);
                return;
            }

            if (recipeId == _ownFinishRecipeId && Time.unscaledTime <= _ownFinishUntil)
            {
                _ownFinishRecipeId = 0;
                return;
            }

            QueueStockPulse(recipeId, StockPulseBaseline(recipeId));
        }

        // On the host the stock already counts the module when the finish call comes; a remote client gets the count
        // just after it. Should a count ever come first, the pulse still plays once the wait runs out.
        private int StockPulseBaseline(int recipeId)
        {
            return _workbench.isServer ? -1 : _workbench.GetStoredCount(recipeId);
        }

        private void QueueStockPulse(int recipeId, int baseline)
        {
            if (!_isOpen)
            {
                return;
            }

            if (_stockPulseRecipeId != 0)
            {
                PlayStockPulse(_stockPulseRecipeId);
            }

            _stockPulseRecipeId = recipeId;
            _stockPulseBaseline = baseline;
            _stockPulseUntil = Time.unscaledTime + _effectSettings.CraftWaitSeconds;
        }

        // Someone else's craft: the rows simply empty, and only the stock cell gets "+1". No name is shown.
        private void PlayStockPulse(int recipeId)
        {
            HandleEffectCued(WorkbenchEffectCue.OtherCrafted);
            WorkbenchItemSlot slot = FindStoredSlot(recipeId);

            if (!slot)
            {
                return;
            }

            _effects.PlayPunch(slot.IconRect, _effectSettings.PunchStrength);
            _effects.PlayPunch(slot.CountRect, _effectSettings.PunchStrength);
            _effects.PlayGain(slot.CountRect, 1);
        }

        // This player's own craft, after the server said yes: the ingredient icons are drawn into the middle of the
        // progress pane, light bursts there leaving the module icon, and the icon flies to its stock cell.
        private void StartCraftShow(int recipeId)
        {
            if (!_workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe))
            {
                ReleaseReserved();
                return;
            }

            RectTransform center = (RectTransform)_rowTemplate.transform.parent;

            // The rows may already read 0 (the new amounts can land first); their icons stay all the same.
            if (_viewedRecipe == recipe)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i].gameObject.activeSelf)
                    {
                        _effects.PlayGather(_rows[i].Icon.sprite, _rows[i].Icon.rectTransform, center);
                    }
                }
            }

            _craftShowRecipeId = recipeId;
            _craftShowIcon = GetRecipeIcon(recipe);
            _craftShowStage = 0;
            _craftShowElapsed = 0f;
        }

        private void TickCraftShow(float deltaTime)
        {
            if (_craftShowRecipeId == 0)
            {
                return;
            }

            _craftShowElapsed += deltaTime;
            float burstAt = _effectSettings.GatherSeconds;
            float flightAt = burstAt + _effectSettings.BurstHoldSeconds;

            if (_craftShowStage == 0 && _craftShowElapsed >= burstAt)
            {
                _craftShowStage = 1;
                _effects.PlayBurst(_craftShowIcon, (RectTransform)_rowTemplate.transform.parent);
                HandleEffectCued(WorkbenchEffectCue.Crafted);
            }
            else if (_craftShowStage == 1 && _craftShowElapsed >= flightAt)
            {
                // By now the new stock count is in. Its cell is put up as an empty frame to land on, unless it is on
                // another page.
                _craftShowStage = 2;
                _isReservedLanding = true;
                RefreshStock();
                WorkbenchItemSlot slot = FindStoredSlot(_craftShowRecipeId);
                _effects.HideBurstIcon();
                _effects.PlayFlight(_craftShowIcon, _effects.BurstIconRect, slot ? slot.IconRect : StockFallbackTarget(),
                    slot ? slot.CountRect : null, WorkbenchEffectCue.ModuleStored);
            }
            else if (_craftShowStage == 2 && _craftShowElapsed >= flightAt + _effectSettings.FlightSeconds)
            {
                // Landed: from here the module shows in the stock and can be taken.
                ReleaseReserved();
                RefreshStock();
                WorkbenchItemSlot slot = FindStoredSlot(_craftShowRecipeId);

                if (slot)
                {
                    _effects.PlayGain(slot.CountRect, 1);
                }

                if (_takeButton && _takeButton.image)
                {
                    _effects.PlayFlash(_takeButton.image, _effectSettings.HighlightColor, 1);
                    _effects.PlayPunch((RectTransform)_takeButton.transform, _effectSettings.PunchStrength);
                }

                _craftShowRecipeId = 0;
            }
        }

        // Taken: from the stock cell into the carry bar. Put back: the reverse, onto its cell put up as an empty frame.
        // Either way the module shows where it lands only when it lands (TickCarryReveal).
        private void PlayCarryMove(int recipeId)
        {
            if (!_workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe))
            {
                ReleaseCarryHolds();
                Refresh();
                return;
            }

            Sprite icon = GetRecipeIcon(recipe);

            if (_isCarryMoveTake)
            {
                _effects.PlayFlight(icon, _carryMoveFrom, _carryIcon.rectTransform, null, WorkbenchEffectCue.ModuleTaken);
            }
            else
            {
                _isReturnLanding = true;
                RefreshStock();
                WorkbenchItemSlot slot = FindStoredSlot(recipeId);
                _effects.PlayFlight(icon, _carryMoveFrom, slot ? slot.IconRect : StockFallbackTarget(), null,
                    WorkbenchEffectCue.ModuleReturned);
            }

            _carryRevealRecipeId = recipeId;
            _carryRevealAt = Time.unscaledTime + _effectSettings.FlightSeconds;
        }

        private void TickCarryReveal()
        {
            if (_carryRevealRecipeId == 0 || Time.unscaledTime < _carryRevealAt)
            {
                return;
            }

            int recipeId = _carryRevealRecipeId;
            bool wasTake = _isCarryHeld;
            ReleaseCarryHolds();
            Refresh();

            if (wasTake)
            {
                _effects.PlayPunch(_carryIcon.rectTransform, _effectSettings.BadgePunchStrength);

                if (_carryText)
                {
                    _effects.PlayFlash(_carryText, _effectSettings.HighlightColor, 1);
                }

                return;
            }

            WorkbenchItemSlot slot = FindStoredSlot(recipeId);

            if (slot)
            {
                _effects.PlayPunch(slot.IconRect, _effectSettings.BadgePunchStrength);
                _effects.PlayPunch(slot.CountRect, _effectSettings.PunchStrength);
                _effects.PlayGain(slot.CountRect, 1);
            }
        }

        private void ReleaseCarryHolds()
        {
            _isCarryHeld = false;
            _returnReservedRecipeId = 0;
            _isReturnLanding = false;
            _carryRevealRecipeId = 0;
        }

        // A count above the one at the click holds this player's own module (crafted, or put back), which is left off
        // until it lands.
        private int ShownStoredCount(int recipeId)
        {
            int stored = _workbench.GetStoredCount(recipeId);
            int shown = stored;

            if (recipeId == _reservedRecipeId && stored > _reservedBaseline)
            {
                shown--;
            }

            if (recipeId == _returnReservedRecipeId && stored > _returnReservedBaseline)
            {
                shown--;
            }

            return Mathf.Max(0, shown);
        }

        private bool IsAwaitingCell(int recipeId)
        {
            return _isReservedLanding && recipeId == _reservedRecipeId || _isReturnLanding && recipeId == _returnReservedRecipeId;
        }

        private void ReleaseReserved()
        {
            _reservedRecipeId = 0;
            _isReservedLanding = false;
        }

        private void TickStockPulse()
        {
            if (_stockPulseRecipeId == 0)
            {
                return;
            }

            if (_workbench.GetStoredCount(_stockPulseRecipeId) > _stockPulseBaseline || Time.unscaledTime > _stockPulseUntil)
            {
                PlayStockPulse(_stockPulseRecipeId);
                _stockPulseRecipeId = 0;
            }
        }

        private WorkbenchItemSlot FindStoredSlot(int recipeId)
        {
            for (int i = 0; i < _storedSlots.Length; i++)
            {
                if (_storedSlots[i].ActionId == recipeId && _storedSlots[i].gameObject.activeInHierarchy)
                {
                    return _storedSlots[i];
                }
            }

            return null;
        }

        // A module whose cell is on another stock page lands on the page button that leads there, else on take.
        private RectTransform StockFallbackTarget()
        {
            if (_nextStockPageButton && _nextStockPageButton.interactable)
            {
                return (RectTransform)_nextStockPageButton.transform;
            }

            if (_previousStockPageButton && _previousStockPageButton.interactable)
            {
                return (RectTransform)_previousStockPageButton.transform;
            }

            return _takeButton ? (RectTransform)_takeButton.transform : null;
        }

        // A deposit that went down without a craft was withdrawn (2026-10-27 decision, R19): the row blinks red, and who
        // took it is not shown. Decided after a short grace, since the host sees a craft's drop before its finish call.
        private void QueueDropFlash(int recipeId, int ingredientIndex)
        {
            if (recipeId == _lastFinishRecipeId && Time.unscaledTime - _lastFinishTime <= _effectSettings.CraftWaitSeconds)
            {
                return;
            }

            if (_dropRows != 0 && _dropRecipeId != recipeId)
            {
                FlushDropFlash();
            }

            if (_dropRows == 0)
            {
                _dropSeenAt = Time.unscaledTime;
            }

            _dropRecipeId = recipeId;
            _dropRows |= 1 << ingredientIndex;
        }

        private void TickDropFlash()
        {
            if (_dropRows != 0 && Time.unscaledTime >= _dropSeenAt + _effectSettings.DropGraceSeconds)
            {
                FlushDropFlash();
            }
        }

        private void FlushDropFlash()
        {
            bool isCraft = _dropRecipeId == _lastFinishRecipeId && _lastFinishTime >= _dropSeenAt - _effectSettings.CraftWaitSeconds;
            int rows = _dropRows;
            _dropRows = 0;

            if (isCraft || !_viewedRecipe || _viewedRecipe.Id != _dropRecipeId)
            {
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if ((rows & (1 << i)) != 0 && _rows[i].gameObject.activeSelf)
                {
                    _effects.PlayFlash(_rows[i].Icon);
                    _effects.PlayFlash(_rows[i].Count);
                }
            }

            HandleEffectCued(WorkbenchEffectCue.Withdrawn);
        }

        private void TickEffects(float deltaTime)
        {
            _effects.Tick(deltaTime);
            TickCraftShow(deltaTime);
            TickStockPulse();
            TickCarryReveal();
            TickDropFlash();

            if (_ownCredit > 0 && Time.unscaledTime > _ownCreditUntil)
            {
                _ownCredit = 0;
            }

            if (_isSketchLayout && _overallFill && _overallShown != _overallTarget)
            {
                _overallShown = Mathf.MoveTowards(_overallShown, _overallTarget, _effectSettings.FillSpeed * deltaTime);
                ApplyOverallFill();
            }
        }

        private void ApplyOverallFill()
        {
            _overallFill.anchorMax = new Vector2(_overallShown, 1f);
            _overallFill.gameObject.SetActive(_overallShown > 0f);
        }

        private WorkbenchIngredientRow FindRow(int resourceId)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].gameObject.activeSelf && _rows[i].ResourceId == resourceId)
                {
                    return _rows[i];
                }
            }

            return null;
        }

        // Stock cells follow the resource icon list one to one (see RefreshStock); a cell off screen does not count.
        private WorkbenchItemSlot FindResourceSlot(int resourceId)
        {
            for (int i = 0; i < _resourceSlots.Length && i < _resourceIcons.Length; i++)
            {
                if (_resourceIcons[i].Resource && _resourceIcons[i].Resource.Id == resourceId && _resourceSlots[i].gameObject.activeInHierarchy)
                {
                    return _resourceSlots[i];
                }
            }

            return null;
        }

        private void HandleEffectCued(WorkbenchEffectCue cue)
        {
            EffectCued?.Invoke(cue);
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
                case InteractionAction.Withdraw:
                    return $"{_pendingRecipeName}에서 뺄 수 없습니다. (넣은 자원이 없거나 인벤토리가 가득 참, 쓰러짐·배 탑승 상태)";
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
                case InteractionAction.Withdraw:
                    return $"{_pendingRecipeName}에서 자원을 1개 뺐습니다.";
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
            _effects.CancelFlash(_message);
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

        // Domain reload is off in the editor, so a count left from the last play would carry over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _openCount = 0;
        }
    }
}
