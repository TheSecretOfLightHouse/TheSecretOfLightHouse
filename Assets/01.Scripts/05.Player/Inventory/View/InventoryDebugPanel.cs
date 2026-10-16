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