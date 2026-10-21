using System.Text;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Workbench.View
{
    // The shared workbench shown as a shoe-rack style shelf (2026-10-20 trial, replaces the screen in JH04): one bay per
    // recipe with a sign, one shelf per ingredient and an empty slot mark for every unit the recipe needs. Deposited
    // units appear as pieces, so progress is read off the shelf instead of counted on a screen, and a piece the mafia
    // takes leaves a gap that flashes on every machine. Finished modules sit on top of their bay until installed.
    // Everything shown follows WorkbenchNet's public state; the keys send the same relay requests the screen did, so
    // the server rules (partial deposit, craft, mafia-only withdraw, existence cap) are unchanged.
    [DisallowMultipleComponent]
    public sealed class WorkbenchRack : MonoBehaviour
    {
        private const float BoardThickness = 0.03f;
        private const float PostThickness = 0.05f;
        private const float SlotMarkHeight = 0.006f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private InteractionTarget _target;
        [Tooltip("Puts every needed resource the player holds onto the bay, or crafts once the bay is full.")]
        [SerializeField]
        private Key _useKey = Key.F;
        [Tooltip("Mafia only, while CanWithdraw is set by the match: takes one piece off the bay.")]
        [SerializeField]
        private Key _withdrawKey = Key.G;

        [Header("Look")]
        [Tooltip("Lit material shared by every part; colors are set per renderer.")]
        [SerializeField]
        private Material _material;
        [SerializeField]
        private RackPieceStyle[] _styles = new RackPieceStyle[0];
        [SerializeField]
        private Vector3 _fallbackPieceSize = new Vector3(0.14f, 0.14f, 0.14f);
        [SerializeField]
        private Color _fallbackPieceColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField]
        private Color _frameColor = new Color(0.36f, 0.26f, 0.18f);
        [SerializeField]
        private Color _slotColor = new Color(0.12f, 0.1f, 0.09f);
        [SerializeField]
        private Color _theftColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField]
        private Color _signColor = new Color(0.16f, 0.2f, 0.28f);
        [SerializeField]
        private Color _readySignColor = new Color(0.85f, 0.65f, 0.15f);
        [SerializeField]
        private Color _moduleColor = new Color(0.45f, 0.75f, 0.85f);

        [Header("Layout")]
        [SerializeField, Min(0.4f)]
        private float _bayWidth = 1f;
        [SerializeField, Min(0f)]
        private float _bayGap = 0.12f;
        [SerializeField, Min(0.2f)]
        private float _depth = 0.5f;
        [SerializeField, Min(0f)]
        private float _baseHeight = 0.12f;
        [Tooltip("Space kept above the tallest piece of each shelf.")]
        [SerializeField, Min(0.02f)]
        private float _levelClearance = 0.14f;
        [SerializeField, Min(1)]
        private int _maxColumns = 5;
        [SerializeField, Min(0.05f)]
        private float _moduleSize = 0.22f;
        [SerializeField, Min(1)]
        private int _maxShownModules = 3;

        [Header("Use")]
        [Tooltip("How close to the front of a bay the local player must stand to use it.")]
        [SerializeField, Min(0.5f)]
        private float _reach = 1.4f;
        [SerializeField, Min(0f)]
        private float _theftFlashSeconds = 4f;
        [SerializeField, Min(0f)]
        private float _messageSeconds = 2.5f;

        [Header("Text")]
        [SerializeField]
        private string[] _osFontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" };
        [SerializeField, Min(0.001f)]
        private float _signCharacterSize = 0.018f;
        [SerializeField, Min(0.001f)]
        private float _promptCharacterSize = 0.014f;

        private readonly StringBuilder _builder = new StringBuilder();
        private MaterialPropertyBlock _block;
        private Font _font;
        private Bay[] _bays = new Bay[0];
        private TextMesh _prompt;
        private Camera _camera;
        private NetworkIdentity _player;
        private InteractionRelay _relay;
        private PlayerInventory _inventory;
        private int _selected = -1;
        private bool _canWithdraw;
        private bool _isStateDirty = true;
        private bool _isPromptDirty = true;
        private string _message;
        private float _messageUntil;
        private float _nextFlashCheck = float.MaxValue;
        // The request this rack is waiting on; results of other senders on the same relay are ignored.
        private bool _isAwaiting;
        private InteractionAction _pendingAction;
        private int _pendingBay = -1;
        private int _pendingResourceId;
        // Filling sends one Deposit per ingredient in turn, since the relay holds one request at a time.
        private int _fillBay = -1;
        private int _fillNext;
        private int _fillMoved;

        public Key WithdrawKey => _withdrawKey;
        // Off while another way of working the bench (the screen) takes the keys. The shelf keeps showing the state.
        public bool IsInputEnabled { get; set; } = true;

        // Set by the match for the local player (mafia, while withdrawing is allowed). Showing the key is not the
        // permission: the server checks the role on every withdraw.
        public bool CanWithdraw
        {
            get => _canWithdraw;
            set
            {
                if (_canWithdraw != value)
                {
                    _canWithdraw = value;
                    _isPromptDirty = true;
                }
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _font = Font.CreateDynamicFontFromOSFont(_osFontNames, 64);
            Build();
        }

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.Changed += HandleWorkbenchChanged;
            }

            _isStateDirty = true;
        }

        private void Update()
        {
            if (_player != NetworkClient.localPlayer)
            {
                BindLocalPlayer(NetworkClient.localPlayer);
            }

            int selected = FindSelectedBay();

            if (selected != _selected)
            {
                _selected = selected;
                _isPromptDirty = true;
            }

            if (_message != null && Time.time >= _messageUntil)
            {
                _message = null;
                _isPromptDirty = true;
            }

            if (Time.time >= _nextFlashCheck)
            {
                RefreshFlashes();
            }

            ReadKeys();
        }

        // Mirror applies a whole sync batch before this, so a craft (stock up, every ingredient back to 0) is seen at
        // once and never mistaken for pieces being taken.
        private void LateUpdate()
        {
            if (_isStateDirty)
            {
                _isStateDirty = false;
                RefreshState();
                _isPromptDirty = true;
            }

            if (_isPromptDirty)
            {
                _isPromptDirty = false;
                RefreshPrompt();
            }

            if (_prompt && _prompt.gameObject.activeSelf)
            {
                PlacePrompt();
            }
        }

        private void OnDisable()
        {
            if (_workbench)
            {
                _workbench.Changed -= HandleWorkbenchChanged;
            }

            BindLocalPlayer(null);
            _selected = -1;
            _fillBay = -1;
            _isAwaiting = false;

            if (_prompt)
            {
                _prompt.gameObject.SetActive(false);
            }
        }

        private void Build()
        {
            ModuleRecipeCatalog catalog = _workbench ? _workbench.Catalog : null;

            if (!catalog || catalog.Validate() != null)
            {
                Debug.LogError($"[{nameof(WorkbenchRack)}] {name}: needs a workbench with a valid recipe catalog.");
                enabled = false;
                return;
            }

            _bays = new Bay[catalog.Count];
            float pitch = _bayWidth + _bayGap;

            for (int i = 0; i < catalog.Count; i++)
            {
                float x = (i - (catalog.Count - 1) * 0.5f) * pitch;
                _bays[i] = BuildBay(catalog[i], x);
            }

            GameObject prompt = new GameObject("Prompt");
            prompt.transform.SetParent(transform, false);
            _prompt = CreateText(prompt, _promptCharacterSize, Color.white);
            prompt.SetActive(false);
        }

        private Bay BuildBay(ModuleRecipe recipe, float x)
        {
            Bay bay = new Bay();
            bay.Recipe = recipe;
            bay.Root = new GameObject(recipe.DisplayName).transform;
            bay.Root.SetParent(transform, false);
            bay.Root.localPosition = new Vector3(x, 0f, 0f);
            bay.Levels = new Level[recipe.IngredientCount];

            float innerWidth = _bayWidth - PostThickness * 2f;
            float y = _baseHeight;
            CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(0f, y - BoardThickness * 0.5f, 0f), new Vector3(_bayWidth, BoardThickness, _depth), Quaternion.identity, _frameColor);

            for (int i = 0; i < bay.Levels.Length; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);
                bay.Levels[i] = BuildLevel(bay.Root, ingredient, innerWidth, y, out float height);
                y += height;
                CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(0f, y - BoardThickness * 0.5f, 0f), new Vector3(_bayWidth, BoardThickness, _depth), Quaternion.identity, _frameColor);
            }

            // Finished modules wait on the top board, the sign stands behind them.
            bay.Modules = new GameObject[_maxShownModules];
            float modulePitch = innerWidth / _maxShownModules;

            for (int i = 0; i < bay.Modules.Length; i++)
            {
                float moduleX = (i - (_maxShownModules - 1) * 0.5f) * modulePitch;
                Vector3 position = new Vector3(moduleX, y + _moduleSize * 0.5f, _depth * 0.15f);
                bay.Modules[i] = CreatePart(bay.Root, PrimitiveType.Cube, position, Vector3.one * _moduleSize, Quaternion.Euler(0f, 20f, 0f), _moduleColor).gameObject;
                bay.Modules[i].SetActive(false);
            }

            float signY = y + _moduleSize + 0.2f;
            bay.Sign = CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(0f, signY, -_depth * 0.5f + 0.02f), new Vector3(_bayWidth, 0.24f, 0.03f), Quaternion.identity, _signColor);
            float top = signY + 0.12f;

            // Posts and back panel tie the shelves into one piece of furniture.
            float postX = (_bayWidth - PostThickness) * 0.5f;
            CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(-postX, top * 0.5f, 0f), new Vector3(PostThickness, top, _depth), Quaternion.identity, _frameColor);
            CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(postX, top * 0.5f, 0f), new Vector3(PostThickness, top, _depth), Quaternion.identity, _frameColor);
            CreatePart(bay.Root, PrimitiveType.Cube, new Vector3(0f, y * 0.5f, -_depth * 0.5f + 0.01f), new Vector3(_bayWidth, y, 0.02f), Quaternion.identity, _frameColor * 0.8f);

            GameObject signText = new GameObject("SignText");
            signText.transform.SetParent(bay.Root, false);
            // TextMesh reads from its -z side, and the rack's front is +z.
            signText.transform.localPosition = new Vector3(0f, signY, -_depth * 0.5f + 0.04f);
            signText.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bay.SignText = CreateText(signText, _signCharacterSize, Color.white);
            bay.SignText.text = recipe.DisplayName;

            BoxCollider body = bay.Root.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, top * 0.5f, 0f);
            body.size = new Vector3(_bayWidth, top, _depth);
            bay.Front = new Vector3(x, 0f, _depth * 0.5f);
            bay.PromptPoint = new Vector3(x, top + 0.35f, _depth * 0.5f);
            return bay;
        }

        // One shelf: an empty mark per needed unit and a hidden piece above each mark.
        private Level BuildLevel(Transform parent, RecipeIngredient ingredient, float innerWidth, float y, out float height)
        {
            FindStyle(ingredient.ResourceId, out RackPieceShape shape, out Vector3 size, out Color color);
            int amount = ingredient.Amount;
            int columns = Mathf.Min(amount, _maxColumns);
            int layers = (amount + columns - 1) / columns;
            float pitch = innerWidth / columns;
            size.x = Mathf.Min(size.x, pitch * 0.9f);
            height = layers * size.y + _levelClearance + BoardThickness;

            Level level = new Level();
            level.ResourceId = ingredient.ResourceId;
            level.Pieces = new GameObject[amount];
            level.Slots = new Renderer[columns];
            level.TheftUntil = new float[columns];

            for (int i = 0; i < columns; i++)
            {
                float x = (i - (columns - 1) * 0.5f) * pitch;
                level.Slots[i] = CreatePart(parent, PrimitiveType.Cube, new Vector3(x, y + SlotMarkHeight * 0.5f, 0f), new Vector3(pitch * 0.86f, SlotMarkHeight, _depth * 0.85f), Quaternion.identity, _slotColor);
            }

            for (int i = 0; i < amount; i++)
            {
                int column = i % columns;
                int layer = i / columns;
                float x = (column - (columns - 1) * 0.5f) * pitch;
                Vector3 position = new Vector3(x, y + SlotMarkHeight + layer * size.y + size.y * 0.5f, 0f);
                level.Pieces[i] = CreatePiece(parent, shape, position, size, color);
                level.Pieces[i].SetActive(false);
            }

            return level;
        }

        private GameObject CreatePiece(Transform parent, RackPieceShape shape, Vector3 position, Vector3 size, Color color)
        {
            switch (shape)
            {
                case RackPieceShape.Rod:
                    // A cylinder's axis is local y; tipped forward it runs front to back.
                    return CreatePart(parent, PrimitiveType.Cylinder, position, new Vector3(size.x, size.z * 0.5f, size.y), Quaternion.Euler(90f, 0f, 0f), color).gameObject;
                case RackPieceShape.Ball:
                    return CreatePart(parent, PrimitiveType.Sphere, position, size, Quaternion.identity, color).gameObject;
                default:
                    return CreatePart(parent, PrimitiveType.Cube, position, size, Quaternion.identity, color).gameObject;
            }
        }

        private Renderer CreatePart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            // The bay carries one collider; part colliders would only catch harvest rays and feet.
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            Renderer partRenderer = part.GetComponent<Renderer>();

            if (_material)
            {
                partRenderer.sharedMaterial = _material;
            }

            SetColor(partRenderer, color);
            return partRenderer;
        }

        private TextMesh CreateText(GameObject owner, float characterSize, Color color)
        {
            TextMesh text = owner.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;

            if (_font)
            {
                text.font = _font;
                owner.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            }

            return text;
        }

        private void FindStyle(int resourceId, out RackPieceShape shape, out Vector3 size, out Color color)
        {
            for (int i = 0; i < _styles.Length; i++)
            {
                if (_styles[i].ResourceId == resourceId)
                {
                    shape = _styles[i].Shape;
                    size = _styles[i].Size;
                    color = _styles[i].Color;
                    return;
                }
            }

            shape = RackPieceShape.Box;
            size = _fallbackPieceSize;
            color = _fallbackPieceColor;
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            target.SetPropertyBlock(_block);
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
            _isAwaiting = false;
            _fillBay = -1;

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }

            if (_inventory)
            {
                _inventory.ResourceCountChanged += HandleResourceCountChanged;
            }

            _isPromptDirty = true;
        }

        // The bay whose front the local player stands nearest, within reach. The player must also be inside the
        // workbench's interaction range, which is what the server checks.
        private int FindSelectedBay()
        {
            if (!IsInputEnabled || !_player || !_relay || !_target || !_workbench.isActiveAndEnabled)
            {
                return -1;
            }

            Vector3 position = _player.transform.position;
            float range = _target.Range;

            if ((position - _target.Position).sqrMagnitude > range * range)
            {
                return -1;
            }

            int best = -1;
            float bestDistance = _reach * _reach;

            for (int i = 0; i < _bays.Length; i++)
            {
                Vector3 offset = position - transform.TransformPoint(_bays[i].Front);
                offset.y = 0f;
                float distance = offset.sqrMagnitude;

                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (_selected < 0 || keyboard == null)
            {
                return;
            }

            if (keyboard[_useKey].wasPressedThisFrame)
            {
                Use(_selected);
            }
            else if (_canWithdraw && keyboard[_withdrawKey].wasPressedThisFrame)
            {
                TakeOne(_selected);
            }
        }

        private void Use(int bayIndex)
        {
            if (IsBusy())
            {
                return;
            }

            ModuleRecipe recipe = _bays[bayIndex].Recipe;

            if (_workbench.IsReadyToCraft(recipe.Id))
            {
                Send(InteractionAction.Craft, bayIndex, recipe.Id, 0, 0);
                return;
            }

            _fillBay = bayIndex;
            _fillNext = 0;
            _fillMoved = 0;

            if (!SendNextDeposit())
            {
                ShowMessage(HoldsNothingNeeded(recipe) ? "이 칸에 올릴 재료가 없습니다." : "지금은 올릴 수 없습니다.");
            }
        }

        // Each ingredient is sent once per key press. Counts read here may lag the server by a message, so the
        // server's clamp decides the real amount.
        private bool SendNextDeposit()
        {
            ModuleRecipe recipe = _bays[_fillBay].Recipe;

            while (_fillNext < recipe.IngredientCount)
            {
                int resourceId = recipe.GetIngredient(_fillNext).ResourceId;
                _fillNext++;
                int held = Held(resourceId);
                int remaining = _workbench.GetRemaining(recipe.Id, resourceId);

                if (held > 0 && remaining > 0 && Send(InteractionAction.Deposit, _fillBay, recipe.Id, resourceId, Mathf.Min(held, remaining)))
                {
                    _pendingResourceId = resourceId;
                    return true;
                }
            }

            _fillBay = -1;
            return false;
        }

        // Takes from the highest shelf that has something, one piece per press.
        private void TakeOne(int bayIndex)
        {
            if (IsBusy())
            {
                return;
            }

            Bay bay = _bays[bayIndex];

            for (int i = bay.Levels.Length - 1; i >= 0; i--)
            {
                if (_workbench.GetDepositedAt(bay.Recipe.Id, i) > 0)
                {
                    int resourceId = bay.Levels[i].ResourceId;

                    if (Send(InteractionAction.Withdraw, bayIndex, bay.Recipe.Id, resourceId, 1))
                    {
                        _pendingResourceId = resourceId;
                    }

                    return;
                }
            }

            ShowMessage("뺄 재료가 없습니다.");
        }

        // The relay holds one request per player, whoever sent it; a filling pass keeps it busy until its last deposit.
        private bool IsBusy()
        {
            if (_relay && _relay.IsAwaitingResult)
            {
                ShowMessage("이전 요청을 처리하는 중입니다.");
                return true;
            }

            _isAwaiting = false;
            _fillBay = -1;
            return false;
        }

        private bool Send(InteractionAction action, int bayIndex, int arg0, int arg1, int arg2)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1, arg2))
            {
                ShowMessage("이전 요청을 처리하는 중입니다.");
                return false;
            }

            _isAwaiting = true;
            _pendingAction = action;
            _pendingBay = bayIndex;
            return true;
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (!_isAwaiting)
            {
                return;
            }

            _isAwaiting = false;
            string recipeName = _pendingBay >= 0 && _pendingBay < _bays.Length ? _bays[_pendingBay].Recipe.DisplayName : string.Empty;

            switch (_pendingAction)
            {
                case InteractionAction.Deposit:
                    if (result.IsSuccess)
                    {
                        _fillMoved++;
                    }

                    if (_fillBay >= 0 && SendNextDeposit())
                    {
                        return;
                    }

                    ShowMessage(_fillMoved > 0 ? $"{recipeName} 칸에 올려놓았습니다." : DescribeRejection(result.Code, "올릴 수 없습니다. (다 찬 재료·제작 상한·쓰러짐·배 탑승)"));
                    break;
                case InteractionAction.Craft:
                    ShowMessage(result.IsSuccess ? $"{recipeName} 완성! 칸 위에 올려 두었습니다." : DescribeRejection(result.Code, "제작할 수 없습니다. (다른 선원이 먼저 제작함·쓰러짐·배 탑승)"));
                    break;
                case InteractionAction.Withdraw:
                    ShowMessage(result.IsSuccess ? $"{ResourceName(_pendingResourceId)} 1개를 몰래 뺐습니다." : DescribeRejection(result.Code, "뺄 수 없습니다. (보유 한도·지금은 뺄 수 없음)"));
                    break;
            }
        }

        private string DescribeRejection(InteractionResultCode code, string rejected)
        {
            switch (code)
            {
                case InteractionResultCode.OutOfRange:
                    return "작업대에서 너무 멉니다.";
                case InteractionResultCode.NotInProgress:
                    return "판이 진행 중일 때만 쓸 수 있습니다.";
                case InteractionResultCode.Rejected:
                    return rejected;
                default:
                    return $"요청을 처리하지 못했습니다. ({code})";
            }
        }

        private void HandleWorkbenchChanged()
        {
            _isStateDirty = true;
        }

        private void HandleResourceCountChanged(int resourceId, int count)
        {
            _isPromptDirty = true;
        }

        private void RefreshState()
        {
            for (int i = 0; i < _bays.Length; i++)
            {
                RefreshBay(_bays[i]);
            }
        }

        private void RefreshBay(Bay bay)
        {
            int recipeId = bay.Recipe.Id;
            int stored = _workbench.GetStoredCount(recipeId);
            // Crafting empties every shelf in the same batch that adds the module; any other drop is a withdraw.
            bool isCraft = stored > bay.ShownStored;

            for (int i = 0; i < bay.Levels.Length; i++)
            {
                Level level = bay.Levels[i];
                int deposited = Mathf.Clamp(_workbench.GetDepositedAt(recipeId, i), 0, level.Pieces.Length);

                if (!isCraft)
                {
                    for (int k = deposited; k < level.Shown; k++)
                    {
                        FlashTheft(level, k % level.Slots.Length);
                    }
                }

                for (int k = 0; k < level.Pieces.Length; k++)
                {
                    level.Pieces[k].SetActive(k < deposited);
                }

                level.Shown = deposited;
            }

            for (int i = 0; i < bay.Modules.Length; i++)
            {
                bay.Modules[i].SetActive(i < stored);
            }

            bay.ShownStored = stored;
            bool isReady = _workbench.IsReadyToCraft(recipeId);

            if (isReady != bay.IsReady)
            {
                bay.IsReady = isReady;
                SetColor(bay.Sign, isReady ? _readySignColor : _signColor);
                bay.SignText.text = isReady ? $"{bay.Recipe.DisplayName}\n제작 가능" : bay.Recipe.DisplayName;
            }
        }

        private void FlashTheft(Level level, int slot)
        {
            if (_theftFlashSeconds <= 0f)
            {
                return;
            }

            float until = Time.time + _theftFlashSeconds;
            level.TheftUntil[slot] = until;
            SetColor(level.Slots[slot], _theftColor);
            _nextFlashCheck = Mathf.Min(_nextFlashCheck, until);
        }

        private void RefreshFlashes()
        {
            float now = Time.time;
            _nextFlashCheck = float.MaxValue;

            for (int i = 0; i < _bays.Length; i++)
            {
                Level[] levels = _bays[i].Levels;

                for (int j = 0; j < levels.Length; j++)
                {
                    Level level = levels[j];

                    for (int k = 0; k < level.TheftUntil.Length; k++)
                    {
                        float until = level.TheftUntil[k];

                        if (until <= 0f)
                        {
                            continue;
                        }

                        if (now >= until)
                        {
                            level.TheftUntil[k] = 0f;
                            SetColor(level.Slots[k], _slotColor);
                        }
                        else
                        {
                            _nextFlashCheck = Mathf.Min(_nextFlashCheck, until);
                        }
                    }
                }
            }
        }

        private void RefreshPrompt()
        {
            if (!_prompt)
            {
                return;
            }

            if (_selected < 0)
            {
                _prompt.gameObject.SetActive(false);
                return;
            }

            Bay bay = _bays[_selected];
            int recipeId = bay.Recipe.Id;
            _builder.Clear();
            _builder.Append(bay.Recipe.DisplayName).Append('\n');

            if (_workbench.IsReadyToCraft(recipeId))
            {
                _builder.Append('[').Append(_useKey).Append("] 제작하기");
            }
            else if (HoldsNothingNeeded(bay.Recipe))
            {
                _builder.Append("이 칸에 올릴 재료가 없습니다");
            }
            else
            {
                _builder.Append('[').Append(_useKey).Append("] 올려놓기 ");
                AppendHeldNeeded(bay.Recipe);
            }

            if (_canWithdraw && HasAnyDeposited(bay))
            {
                _builder.Append("\n[").Append(_withdrawKey).Append("] 몰래 1개 빼기");
            }

            if (_message != null)
            {
                _builder.Append('\n').Append(_message);
            }

            _prompt.text = _builder.ToString();
            _prompt.gameObject.SetActive(true);
            PlacePrompt();
        }

        private void PlacePrompt()
        {
            if (!_camera || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            _prompt.transform.position = transform.TransformPoint(_bays[_selected].PromptPoint);

            // A TextMesh faces away from its forward, so matching the camera's rotation keeps it readable.
            if (_camera)
            {
                _prompt.transform.rotation = _camera.transform.rotation;
            }
        }

        private void AppendHeldNeeded(ModuleRecipe recipe)
        {
            bool isFirst = true;

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);
                int amount = Mathf.Min(Held(ingredient.ResourceId), _workbench.GetRemaining(recipe.Id, ingredient.ResourceId));

                if (amount <= 0)
                {
                    continue;
                }

                _builder.Append(isFirst ? string.Empty : " · ").Append(ingredient.Resource.DisplayName).Append(' ').Append(amount);
                isFirst = false;
            }
        }

        private bool HoldsNothingNeeded(ModuleRecipe recipe)
        {
            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                int resourceId = recipe.GetIngredient(i).ResourceId;

                if (Held(resourceId) > 0 && _workbench.GetRemaining(recipe.Id, resourceId) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasAnyDeposited(Bay bay)
        {
            for (int i = 0; i < bay.Levels.Length; i++)
            {
                if (_workbench.GetDepositedAt(bay.Recipe.Id, i) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private int Held(int resourceId)
        {
            return _inventory ? _inventory.GetCount(resourceId) : 0;
        }

        private string ResourceName(int resourceId)
        {
            for (int i = 0; i < _bays.Length; i++)
            {
                ModuleRecipe recipe = _bays[i].Recipe;
                int index = recipe.IndexOf(resourceId);

                if (index >= 0)
                {
                    return recipe.GetIngredient(index).Resource.DisplayName;
                }
            }

            return "재료";
        }

        private void ShowMessage(string message)
        {
            _message = message;
            _messageUntil = Time.time + _messageSeconds;
            _isPromptDirty = true;
        }

        private sealed class Bay
        {
            public ModuleRecipe Recipe;
            public Transform Root;
            public Level[] Levels;
            public GameObject[] Modules;
            public Renderer Sign;
            public TextMesh SignText;
            // Rack-local points: where the player stands to use the bay, and where its prompt floats.
            public Vector3 Front;
            public Vector3 PromptPoint;
            public int ShownStored;
            public bool IsReady;
        }

        private sealed class Level
        {
            public int ResourceId;
            public GameObject[] Pieces;
            // One empty mark per column; pieces of later layers stack on the same marks.
            public Renderer[] Slots;
            public float[] TheftUntil;
            public int Shown;
        }
    }
}
