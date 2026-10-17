using System.Collections.Generic;
using System.Text;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;
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
    private bool _isSubscribed;
    private string _contents = "보유 자원 없음";
    private Vector2 _scrollPosition;
    private string _testResourceId = "1";
    private string _testRequestedAmount = "4";
    private string _testResult = "";
    private double _nextTestTime;
    private int _serverTestResourceId;
    private int _serverTestRemaining;
    private bool _hasServerTest;

    private const double TestIntervalSeconds = 0.25;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
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
        if (!isLocalPlayer || !_showPanel)
        {
            return;
        }

        float width = Mathf.Min(_width, Screen.width);
        float height = Mathf.Min(_height, Screen.height);
        float x = Mathf.Max(0f, Screen.width - width - _margin);
        float y = Mathf.Clamp(_margin, 0f, Screen.height - height);

        GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
        GUILayout.Label("내 인벤토리 · 동기화 수량");
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
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
        Unsubscribe();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Subscribe();
    }

    public override void OnStopLocalPlayer()
    {
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
        _contents = "보유 자원 없음";
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
}
