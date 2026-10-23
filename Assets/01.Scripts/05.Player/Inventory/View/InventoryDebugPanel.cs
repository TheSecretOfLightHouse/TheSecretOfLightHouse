using System.Collections.Generic;
using System.Text;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
public sealed class InventoryDebugPanel : NetworkBehaviour
{
    [SerializeField]
    private ResourceCatalog _resourceCatalog;

    [SerializeField]
    private bool _showPanel = true;

    [SerializeField, Min(160f)]
    private float _width = 300f;

    [SerializeField, Min(100f)]
    private float _height = 240f;

    [SerializeField, Min(0f)]
    private float _margin = 16f;

    private readonly StringBuilder _builder = new StringBuilder();
    private PlayerInventory _inventory;
    private PlayerController _player;
    private InteractionRelay _relay;
    private bool _isInstalling;
    private string _installResult = "";
    private BoatController _displayedBoat;
    private BoatModuleEquipment _boatEquipment;
    private bool _isSubscribed;
    private string _contents = "보유 자원 없음";
    private int _displayedModuleId;
    private ModuleRecipe _displayedModuleRecipe;
    private string _moduleContents = "없음";
    private Vector2 _scrollPosition;
    private string _testResourceId = "1";
    private string _testRequestedAmount = "4";
    private string _testResult = "";
    private double _nextTestTime;
    private int _serverTestResourceId;
    private int _serverTestRemaining;
    private bool _hasServerTest;
    private Lighthouse.Player.Inventory.View.InventoryChestScreen _chestView;

    private const double TestIntervalSeconds = 0.25;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _player = GetComponent<PlayerController>();
        _relay = GetComponent<InteractionRelay>();
        _showPanel = false;
    }

    private void OnEnable()
    {
        if (isLocalPlayer)
        {
            Subscribe();
        }
    }

    private void OnGUI()
    {
        if (!isLocalPlayer || !_showPanel || _chestView)
        {
            return;
        }

        float width = Mathf.Min(_width, Screen.width);
        float height = Mathf.Min(_height, Screen.height);
        float x = Mathf.Max(0f, Screen.width - width - _margin);
        float y = Mathf.Clamp(_margin, 0f, Screen.height - height);

        GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
        GUILayout.Label("내 인벤토리 · I / Tab으로 닫기");
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        RefreshModuleContents();
        GUILayout.Label("운반 중인 모듈");
        GUILayout.Label(_moduleContents);
        DrawInstallButton();
        DrawInstalledModules();
        GUILayout.Label("보유 자원");
        GUILayout.Label(_contents);
        GUILayout.EndScrollView();
        if (Debug.isDebugBuild)
        {
            GUILayout.Label("차감 테스트 · 작업대 투입 아님");
            GUILayout.BeginHorizontal();
            GUILayout.Label("자원 ID");
            _testResourceId = GUILayout.TextField(_testResourceId, 10);
            GUILayout.Label("총 필요량");
            _testRequestedAmount = GUILayout.TextField(_testRequestedAmount, 10);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("새 투입 테스트 시작"))
            {
                if (int.TryParse(_testResourceId, out int id) && id > 0 &&
                    int.TryParse(_testRequestedAmount, out int amount) && amount > 0)
                {
                    CmdBeginTest(id, amount);
                }
                else
                {
                    _testResult = "양수인 자원 ID와 요청량을 입력하세요.";
                }
            }

            if (GUILayout.Button("남은 필요량만큼 투입"))
            {
                CmdTestRemove();
            }

            GUILayout.Label(_testResult);
        }
        GUILayout.EndArea();
    }

    private void OnDisable()
    {
        _showPanel = false;
        Unsubscribe();
    }

    public void OnInventory(InputValue value)
    {
        if (!isLocalPlayer || !isActiveAndEnabled || !value.isPressed)
        {
            return;
        }

        if (_chestView)
        {
            _chestView.Toggle();
            return;
        }
        _showPanel = !_showPanel;
    }

    public void BindChestView(Lighthouse.Player.Inventory.View.InventoryChestScreen view)
    {
        _chestView = view;
        _showPanel = false;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Subscribe();
    }

    public override void OnStopLocalPlayer()
    {
        _showPanel = false;
        Unsubscribe();
        base.OnStopLocalPlayer();
    }

    private void Subscribe()
    {
        if (_isSubscribed || !_inventory)
        {
            return;
        }

        _inventory.ResourceCountChanged += HandleResourceCountChanged;
        _inventory.InventoryReset += HandleInventoryReset;
        if (_relay)
        {
            _relay.ResultReceived += HandleInstallResult;
        }
        _isSubscribed = true;
        RebuildContents();
    }

    private void Unsubscribe()
    {
        if (_isSubscribed && _inventory)
        {
            _inventory.ResourceCountChanged -= HandleResourceCountChanged;
            _inventory.InventoryReset -= HandleInventoryReset;
        }

        _isSubscribed = false;
        if (_relay)
        {
            _relay.ResultReceived -= HandleInstallResult;
        }
        _isInstalling = false;
        _installResult = "";
        _contents = "보유 자원 없음";
    }

    private void DrawInstallButton()
    {
        if (!_displayedModuleRecipe || !_displayedModuleRecipe.IsBoatModule)
        {
            if (!string.IsNullOrEmpty(_installResult))
            {
                GUILayout.Label(_installResult);
            }
            return;
        }

        bool canInstall = _player && !_player.IsDowned && _player.IsOnBoat
            && _player.CurrentBoat && _player.CurrentBoat == _player.OwnedBoat
            && _player.CurrentBoat.netId != 0 && _relay && NetworkClient.isConnected
            && NetworkClient.ready && !_relay.IsAwaitingResult && !_isInstalling;
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && canInstall;
        if (GUILayout.Button("내 배에 장착"))
        {
            RequestInstallModule();
        }
        GUI.enabled = wasEnabled;

        if (!_player || !_player.IsOnBoat)
        {
            GUILayout.Label("자기 배에 탑승한 뒤 장착할 수 있습니다.");
        }
        else if (!NetworkClient.ready)
        {
            GUILayout.Label("네트워크 접속 준비가 필요합니다.");
        }
        else if (_relay && _relay.IsAwaitingResult)
        {
            GUILayout.Label("요청 결과를 기다리는 중입니다.");
        }

        if (!string.IsNullOrEmpty(_installResult))
        {
            GUILayout.Label(_installResult);
        }
    }

    private void RequestInstallModule()
    {
        // Subscribe before sending: a host may receive the response in the same frame.
        _isInstalling = true;
        _installResult = "장착 요청 중...";
        if (!_relay.TrySendToNetworkTarget(_player.CurrentBoat.netId,
            InteractionAction.InstallModule, _inventory.ModuleRecipeId))
        {
            _isInstalling = false;
            _installResult = "장착 요청을 보내지 못했습니다.";
        }
    }

    private void HandleInstallResult(InteractionResult result)
    {
        if (!_isInstalling)
        {
            return;
        }

        _isInstalling = false;
        _installResult = result.IsSuccess
            ? "배에 모듈을 장착했습니다."
            : $"장착이 거절되었습니다: {result.Code}";
    }

    private void HandleResourceCountChanged(int id, int count)
    {
        RebuildContents();
    }

    private void HandleInventoryReset()
    {
        _contents = "보유 자원 없음";
    }

    [Command]
    private void CmdBeginTest(int id, int requiredAmount)
    {
        if (!Debug.isDebugBuild || !_inventory || !_resourceCatalog || id <= 0 || requiredAmount <= 0 ||
            NetworkTime.time < _nextTestTime)
        {
            return;
        }

        if (!_resourceCatalog.TryGetById(id, out ResourceDefinition resource))
        {
            return;
        }

        _nextTestTime = NetworkTime.time + TestIntervalSeconds;
        _serverTestResourceId = id;
        _serverTestRemaining = requiredAmount;
        _hasServerTest = true;
        TargetTestRemoveResult(connectionToClient, id, 0, _inventory.GetCount(id), requiredAmount);
    }

    [Command]
    private void CmdTestRemove()
    {
        if (!Debug.isDebugBuild || !_inventory || !_hasServerTest ||
            NetworkTime.time < _nextTestTime)
        {
            return;
        }

        _nextTestTime = NetworkTime.time + TestIntervalSeconds;
        int id = _serverTestResourceId;
        int removed = _serverTestRemaining > 0
            ? _inventory.TryRemoveTo(id, _serverTestRemaining)
            : 0;
        _serverTestRemaining -= removed;
        int remaining = _inventory.GetCount(id);
        TargetTestRemoveResult(connectionToClient, id, removed, remaining, _serverTestRemaining);
    }

    [TargetRpc]
    private void TargetTestRemoveResult(NetworkConnectionToClient target, int id,
        int removed, int remaining, int needed)
    {
        _testResult = $"ID {id} / 이번 투입 {removed} / 보유 {remaining} / 남은 필요량 {needed}";
    }

    private void RebuildContents()
    {
        _builder.Clear();
        foreach (KeyValuePair<int, int> entry in _inventory.Counts)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            string resourceName = "자원";
            if (_resourceCatalog && _resourceCatalog.TryGetById(entry.Key, out ResourceDefinition resource))
            {
                resourceName = resource.DisplayName;
            }

            _builder.Append(resourceName).Append(" (ID ").Append(entry.Key).Append("): ").Append(entry.Value).AppendLine();
        }

        _contents = _builder.Length == 0 ? "보유 자원 없음" : _builder.ToString();
    }

    private void RefreshModuleContents()
    {
        int recipeId = _inventory.ModuleRecipeId;
        ModuleRecipe recipe = _inventory.GetModuleRecipe();
        if (_displayedModuleId == recipeId && _displayedModuleRecipe == recipe)
        {
            return;
        }

        _displayedModuleId = recipeId;
        _displayedModuleRecipe = recipe;
        _moduleContents = recipeId == 0
            ? "없음"
            : recipe
                ? $"{recipe.DisplayName} (ID {recipeId}): 1개"
                : $"모듈 (ID {recipeId}): 1개";
    }

    private void DrawInstalledModules()
    {
        BoatController ownedBoat = _player ? _player.OwnedBoat : null;
        if (_displayedBoat != ownedBoat)
        {
            _displayedBoat = ownedBoat;
            _boatEquipment = ownedBoat ? ownedBoat.GetComponent<BoatModuleEquipment>() : null;
        }

        GUILayout.Label("내 배에 장착된 모듈");
        if (!_boatEquipment)
        {
            GUILayout.Label("배 장착 정보 없음");
            return;
        }

        if (_boatEquipment.InstalledCount == 0)
        {
            GUILayout.Label("없음");
            return;
        }

        if (_boatEquipment.HasModule(BoatModuleType.Searchlight))
        {
            GUILayout.Label("탐조등 · 장착됨");
        }

        if (_boatEquipment.HasModule(BoatModuleType.Dash))
        {
            GUILayout.Label("대쉬 · 장착됨");
        }

        if (_boatEquipment.HasModule(BoatModuleType.DepthAlarm))
        {
            GUILayout.Label("장애물 경보 · 장착됨");
        }

        if (_boatEquipment.HasModule(BoatModuleType.Stabilizer))
        {
            GUILayout.Label("선체 안정기 · 장착됨");
        }
    }
}
