using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Player.Boat;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using System.Collections.Generic;
using UnityEngine;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.View;
using Lighthouse.Player.Recovery.View;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerWreckRecovery : NetworkBehaviour
{
    private const double CompletionRetryInterval = 1d;
    private const float ShoreDropHeightOffset = 0.25f;

    [SerializeField, Min(1f)]
    private float _recoveryDuration = 90f;

    [Header("Rescue flare (tunable initial values)")]
    [SerializeField, Min(0.1f)] private float _flareCooldown = 15f;
    [SerializeField, Min(0.1f)] private float _flareLifetime = 8f;
    [SerializeField, Min(0.1f)] private float _flareDirectionDuration = 5f;
    [SerializeField, Min(1f)] private float _flareHeight = 25f;
    [SerializeField, Min(0f)] private float _flareDescentSpeed = 1.2f;
    [SyncVar] private double _nextFlareTime;
    [SyncVar] private bool _isFlareAvailable;
    private WreckFlareView _flareView;
    private TowerMatchNet _flareMatch;

    private PlayerController _player;
    private bool _isRecovering;
    private Vector3 _wreckPosition;
    private double _recoveryDeadline;

    private bool _isCompletingRecovery;
    private double _nextCompletionRetryTime;
    private WreckRecoveryResult _recoveryResult;

    private PlayerBoatSpawner _boatSpawner;

    private Transform _boatSpawnPoint;

    private PlayerInventory _inventory;
    private PlayerInventoryDropper _dropper;
    private ResourceDropSpawner _resourceDropSpawner;
    private readonly List<ResourceDropHandle> _lockedShoreDrops = new List<ResourceDropHandle>();
    private bool _hasSettledInventory;
    private uint _actorId;

    private InteractionTarget _rescueTarget;

    private static readonly List<PlayerWreckRecovery> ActiveRecoveries = new List<PlayerWreckRecovery>();

    private readonly List<KeyValuePair<int, int>> _plannedResources = new List<KeyValuePair<int, int>>();

    private bool _hasDropPlan;
    private int _plannedModuleRecipeId;
    private uint _plannedModuleId;

    private bool _hasDroppedResources;

    private bool _hasDiscardedModule;

    private WreckDropItem[] _resourceDrops = System.Array.Empty<WreckDropItem>();

    private ulong _nextDropBatchId;

    private readonly Queue<KeyValuePair<ulong, WreckDropItem[]>> _pendingDrops = new Queue<KeyValuePair<ulong, WreckDropItem[]>>();

    public static IReadOnlyList<PlayerWreckRecovery> Active => ActiveRecoveries;

    public PlayerController Player => _player;
    public InteractionTarget RescueTarget => _rescueTarget;
    public double FlareCooldownRemaining => System.Math.Max(0d, _nextFlareTime - NetworkTime.time);
    public bool IsFlareHudAvailable => isActiveAndEnabled && isLocalPlayer && _player && _player.IsDowned
        && _isFlareAvailable && IsFlareMatchInProgress();

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _inventory = GetComponent<PlayerInventory>();
        _dropper = GetComponent<PlayerInventoryDropper>();
        _resourceDropSpawner = GetComponent<ResourceDropSpawner>();
        _rescueTarget = GetComponent<InteractionTarget>();
    }

    private void OnEnable()
    {
        ActiveRecoveries.Add(this);
        _player.DownedChanged += HandleDownedChanged;
        _player.WreckFlareRequested += HandleFlareRequested;
    }

    private void Start()
    {
        HandleDownedChanged(_player.IsDowned);
    }

    private void OnDisable()
    {
        ActiveRecoveries.Remove(this);
        if (_player)
        {
            _player.DownedChanged -= HandleDownedChanged;
            _player.WreckFlareRequested -= HandleFlareRequested;
        }

        if (_rescueTarget)
        {
            _rescueTarget.enabled = false;
        }
    }

    private void HandleDownedChanged(bool isDowned)
    {
        if (_rescueTarget)
        {
            _rescueTarget.enabled = isDowned;
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        _flareView = gameObject.AddComponent<WreckFlareView>();
        _flareView.Initialize(this);
    }

    public bool IsFlareMatchInProgress()
    {
        LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
        if (!manager || !NetworkClient.active && !NetworkServer.active)
        {
            return false;
        }
        if (NetworkServer.active && manager.IsMatchFinished)
        {
            return false;
        }
        if (manager.IsSessionFlowEnabled
            && (NetworkServer.active ? manager.Phase : manager.ClientState.Phase) != MatchPhase.InProgress)
        {
            return false;
        }
        if (!_flareMatch)
        {
            if (WorldServices.TryGet<TowerNet>(out TowerNet tower) && tower)
            {
                _flareMatch = tower.GetComponent<TowerMatchNet>();
            }
            else
            {
                // Fixed test scenes may not register a facility in WorldServices.
                var spawned = NetworkServer.active ? NetworkServer.spawned : NetworkClient.spawned;
                foreach (NetworkIdentity identity in spawned.Values)
                {
                    if (identity && identity.TryGetComponent(out _flareMatch))
                    {
                        break;
                    }
                }
            }
        }
        return !_flareMatch || _flareMatch.Phase != TowerPhase.Ended;
    }

    private void HandleFlareRequested()
    {
        if (!isLocalPlayer || !_player.CanSimulateLocalWorld || !IsFlareHudAvailable
            || SessionScreen.IsGameplayOverlayOpen || FlareCooldownRemaining > 0d)
        {
            return;
        }
        CmdRequestFlare();
    }

    [Command]
    private void CmdRequestFlare()
    {
        if (!_player || !_player.isActiveAndEnabled || !_player.IsDowned
            || !_isRecovering || _isCompletingRecovery || !_isFlareAvailable
            || NetworkTime.time >= _recoveryDeadline || NetworkTime.time < _nextFlareTime
            || !IsFlareMatchInProgress())
        {
            return;
        }

        _nextFlareTime = NetworkTime.time + Mathf.Max(0.1f, _flareCooldown);
        Vector3 origin = transform.position + Vector3.up;
        // Route through each recipient's own identity, independent of source visibility or distance.
        // Only the TargetRpc creates visuals, so the listen host also receives exactly one copy.
        foreach (NetworkConnectionToClient recipient in NetworkServer.connections.Values)
        {
            if (recipient != null && recipient.isReady && recipient.identity
                && recipient.identity.TryGetComponent(out PlayerWreckRecovery recovery))
            {
                recovery.TargetReceiveFlare(recipient, netId, origin, NetworkTime.time,
                    _flareLifetime, _flareDirectionDuration, _flareHeight, _flareDescentSpeed);
            }
        }
    }

    [TargetRpc]
    private void TargetReceiveFlare(NetworkConnectionToClient recipient, uint sourceId,
        Vector3 origin, double firedAt, float lifetime, float directionDuration, float height, float descentSpeed)
    {
        if (_flareView && IsFlareMatchInProgress())
        {
            _flareView.ShowFlare(origin, firedAt, lifetime, directionDuration,
                height, descentSpeed, sourceId != netId);
        }
    }

    [ServerCallback]
    private void Update()
    {
        if (!_isRecovering)
        {
            return;
        }

        if (_isCompletingRecovery)
        {
            if (NetworkTime.time >= _nextCompletionRetryTime)
            {
                _nextCompletionRetryTime = NetworkTime.time + CompletionRetryInterval;
                TryFinishRecovery();
            }
            return;
        }

        if (NetworkTime.time < _recoveryDeadline)
        {
            return;
        }

        TryBeginCompletion(WreckRecoveryResult.TimedOut);
    }

    [Server]
    public void ConfigureRecovery(PlayerBoatSpawner boatSpawner, Transform boatSpawnPoint, uint actorId)
    {
        _boatSpawner = boatSpawner;
        _boatSpawnPoint = boatSpawnPoint;
        _actorId = actorId;
    }

    [Server]
    public bool TryBeginRecovery(Vector3 wreckPosition)
    {
        BoatController wreckedBoat = _player.OwnedBoat;
        if (_isRecovering || !wreckedBoat || wreckedBoat.Owner != _player
            || !wreckedBoat.IsWrecked || !wreckedBoat.IsOccupied
            || _player.CurrentBoat != wreckedBoat)
        {
            return false;
        }

        _player.SetDowned(true);
        if (!wreckedBoat.TryDetachForWreck(_player))
        {
            _player.SetDowned(false);
            return false;
        }

        _isRecovering = true;
        _isFlareAvailable = true;
        _nextFlareTime = 0d;
        _wreckPosition = wreckPosition;
        _recoveryDeadline = NetworkTime.time + _recoveryDuration;
        _hasSettledInventory = false;
        _hasDropPlan = false;
        _plannedResources.Clear();
        _plannedModuleRecipeId = 0;
        _plannedModuleId = 0;
        _hasDroppedResources = false;
        _hasDiscardedModule = false;
        _resourceDrops = System.Array.Empty<WreckDropItem>();
        return true;
    }

    [Server]
    public bool TryBeginCompletion(WreckRecoveryResult result)
    {
        if (!_isRecovering || _isCompletingRecovery)
        {
            return false;
        }

        if (result != WreckRecoveryResult.Rescued && result != WreckRecoveryResult.TimedOut)
        {
            return false;
        }

        if (!_inventory || !_dropper || !_boatSpawner || !_boatSpawnPoint || !_boatSpawner.PlayerRecoveryPoint)
        {
            return false;
        }

        BoatController wreckedBoat = _player.OwnedBoat;

        if (!wreckedBoat || !wreckedBoat.IsWrecked || wreckedBoat.Owner != _player || !_player.IsDowned
            || wreckedBoat.IsOccupied || _player.IsOnBoat)
        {
            return false;
        }

        _isCompletingRecovery = true;
        _isFlareAvailable = false;
        _nextCompletionRetryTime = NetworkTime.time + CompletionRetryInterval;
        _recoveryResult = result;

        if (!TryFinishRecovery())
        {
            return false;
        }

        return true;
    }

    [Server]
    private bool TrySettleRecoveryInventory()
    {
        if (!_isCompletingRecovery)
        {
            return false;
        }

        if (_hasSettledInventory)
        {
            return true;
        }

        if (_nextDropBatchId == ulong.MaxValue)
        {
            return false;
        }

        if (!TryPrepareDropPlan())
        {
            return false;
        }

        if (!TryDropPlannedResources())
        {
            return false;
        }

        if (!TryDiscardPlannedModule())
        {
            return false;
        }

        if (_resourceDrops.Length > 0)
        {
            ulong batchId = ++_nextDropBatchId;

            _pendingDrops.Enqueue(
                new KeyValuePair<ulong, WreckDropItem[]>(
                    batchId,
                    _resourceDrops));
        }

        _hasSettledInventory = true;
        return true;
    }

    [Server]
    private bool TryFinishRecovery()
    {
        if (!_isCompletingRecovery || !_player || !_boatSpawner || !_boatSpawnPoint || !_boatSpawner.PlayerRecoveryPoint)
        {
            return false;
        }

        BoatController wreckedBoat = _player.OwnedBoat;

        if (!wreckedBoat || !wreckedBoat.IsWrecked || wreckedBoat.Owner != _player || !_player.IsDowned
            || wreckedBoat.IsOccupied || _player.IsOnBoat)
        {
            return false;
        }

        if (!_boatSpawner.TryCreateRecoveryBoat(_player, _boatSpawnPoint, out BoatController replacementBoat))
        {
            return false;
        }

        if (!TrySettleRecoveryInventory())
        {
            NetworkServer.Destroy(replacementBoat.gameObject);
            return false;
        }

        Vector3 recoveryPosition = _boatSpawner.PlayerRecoveryPoint.position;

        if (!wreckedBoat.TryDisembarkForRecovery(_player, recoveryPosition))
        {
            NetworkServer.Destroy(replacementBoat.gameObject);
            return false;
        }

        if (!_player.TryReplaceWreckedBoat(wreckedBoat, replacementBoat))
        {
            NetworkServer.Destroy(replacementBoat.gameObject);
            return false;
        }

        NetworkServer.Destroy(wreckedBoat.gameObject);

        _player.SetDowned(false);
        _isRecovering = false;
        _isCompletingRecovery = false;
        _nextCompletionRetryTime = 0d;
        _recoveryDeadline = 0d;

        return true;
    }

    [Server]
    private bool TryPrepareDropPlan()
    {
        if (!_isCompletingRecovery || !_inventory)
        {
            return false;
        }

        if (_hasDropPlan)
        {
            return true;
        }

        if (!_inventory.TryBuildWreckDropPlan(_recoveryResult, _plannedResources, out _plannedModuleRecipeId,out _plannedModuleId))
        {
            return false;
        }

        _hasDropPlan = true;
        return true;
    }

    [Server]
    private bool TryDropPlannedResources()
    {
        if (!_isCompletingRecovery || !_hasDropPlan || !_dropper)
        {
            return false;
        }

        if (_hasDroppedResources)
        {
            return true;
        }

        bool isUnrescued = _recoveryResult == WreckRecoveryResult.TimedOut;
        _lockedShoreDrops.Clear();

        // Unrescued: lay the stacks on a shore first, locked, and only unlock them once the inventory has really
        // given them up, so a failure at either step never loses or duplicates resources.
        if (isUnrescued && !TrySpawnLockedShoreDrops())
        {
            return false;
        }

        if (!_dropper.TryDropResources(_plannedResources, _wreckPosition, out WreckDropItem[] droppedItems))
        {
            WithdrawLockedShoreDrops();
            return false;
        }

        for (int i = 0; i < _lockedShoreDrops.Count; i++)
        {
            _resourceDropSpawner.TryUnlockDrop(_lockedShoreDrops[i]);
        }

        _lockedShoreDrops.Clear();
        _resourceDrops = droppedItems;
        _hasDroppedResources = true;

        return true;
    }

    [Server]
    private bool TrySpawnLockedShoreDrops()
    {
        if (_plannedResources.Count == 0)
        {
            return true;
        }

        if (!_resourceDropSpawner || !WorldServices.TryGet(out IShoreDropLocator locator))
        {
            return false;
        }

        for (int i = 0; i < _plannedResources.Count; i++)
        {
            KeyValuePair<int, int> entry = _plannedResources[i];
            ShoreDropResult spot = locator.GetDropPoint(new ShoreDropRequest(_wreckPosition, DebrisKind.Resource, i));

            if (!spot.IsSuccess
                || !_resourceDropSpawner.TrySpawnDrop(
                    spot.Position + Vector3.up * ShoreDropHeightOffset, entry.Key, entry.Value, true, out ResourceDropHandle handle))
            {
                WithdrawLockedShoreDrops();
                return false;
            }

            _lockedShoreDrops.Add(handle);
        }

        return true;
    }

    [Server]
    private void WithdrawLockedShoreDrops()
    {
        for (int i = 0; i < _lockedShoreDrops.Count; i++)
        {
            _resourceDropSpawner.TryWithdrawDrop(_lockedShoreDrops[i]);
        }

        _lockedShoreDrops.Clear();
    }

    [Server]
    private bool TryDiscardPlannedModule()
    {
        if (!_isCompletingRecovery || !_hasDropPlan || !_inventory)
        {
            return false;
        }

        if (_hasDiscardedModule)
        {
            return true;
        }

        if (_plannedModuleRecipeId == 0 && _plannedModuleId == 0)
        {
            _hasDiscardedModule = true;
            return true;
        }

        if (_plannedModuleRecipeId <= 0 || _plannedModuleId == 0)
        {
            return false;
        }

        int resourceCount = _resourceDrops.Length;
        var results = new WreckDropItem[resourceCount + 1];

        System.Array.Copy(_resourceDrops, results, resourceCount);

        results[resourceCount] = new WreckDropItem(
            true,
            _plannedModuleRecipeId,
            1,
            _wreckPosition);

        // Unrescued: the module lands on a shore instead of being lost; a failed attempt leaves it in the hands to retry.
        bool isModuleHandled = _recoveryResult == WreckRecoveryResult.TimedOut
            ? _inventory.TryDropWreckModuleToShore(_plannedModuleId, _wreckPosition)
            : _inventory.TryDiscardWreckModule(_plannedModuleId);

        if (!isModuleHandled)
        {
            return false;
        }

        _resourceDrops = results;
        _hasDiscardedModule = true;

        return true;
    }

    [Server]
    public bool TryCopyResourceDropsTo(List<WreckDropItem> results, out ulong batchId)
    {
        batchId = 0;

        if (results == null)
        {
            return false;
        }

        results.Clear();

        if (!isServer || _pendingDrops.Count == 0)
        {
            return false;
        }

        KeyValuePair<ulong, WreckDropItem[]> batch =
            _pendingDrops.Peek();

        batchId = batch.Key;

        for (int i = 0; i < batch.Value.Length; i++)
        {
            results.Add(batch.Value[i]);
        }

        return true;
    }

    [Server]
    public bool TryAcknowledgeDrops(ulong batchId)
    {
        if (!isServer || _pendingDrops.Count == 0)
        {
            return false;
        }

        if (_pendingDrops.Peek().Key != batchId)
        {
            return false;
        }

        _pendingDrops.Dequeue();
        return true;
    }

    [ContextMenu("Test/Complete Rescue")]
    private void TestCompleteRescue()
    {
        if (!isServer)
        {
            return;
        }

        bool succeeded =TryBeginCompletion(WreckRecoveryResult.Rescued);

        Debug.Log(
            $"[{nameof(PlayerWreckRecovery)}] Rescue completed: {succeeded}",
            this);
    }

#if UNITY_EDITOR
    private ulong _testReadBatchId;

    [ContextMenu("Test/Read Pending Drops")]
    private void TestReadPendingDrops()
    {
        if (!isServer)
        {
            return;
        }

        var results = new List<WreckDropItem>();

        if (!TryCopyResourceDropsTo(results, out ulong batchId))
        {
            _testReadBatchId = 0;

            Debug.Log(
                $"[{nameof(PlayerWreckRecovery)}] No pending drops.",
                this);

            return;
        }

        _testReadBatchId = batchId;

        Debug.Log(
            $"[{nameof(PlayerWreckRecovery)}] "
            + $"Batch={batchId}, Count={results.Count}",
            this);

        foreach (WreckDropItem item in results)
        {
            Debug.Log(
                $"[{nameof(PlayerWreckRecovery)}] "
                + $"IsModule={item.IsModule}, "
                + $"ItemId={item.ItemId}, "
                + $"Amount={item.Amount}, "
                + $"Position={item.Position}",
                this);
        }
    }

    [ContextMenu("Test/Acknowledge Read Drops")]
    private void TestAcknowledgeReadDrops()
    {
        if (!isServer || _testReadBatchId == 0)
        {
            return;
        }

        bool succeeded = TryAcknowledgeDrops(_testReadBatchId);

        Debug.Log(
            $"[{nameof(PlayerWreckRecovery)}] "
            + $"Acknowledge Batch={_testReadBatchId}: {succeeded}",
            this);

        if (succeeded)
        {
            _testReadBatchId = 0;
        }
    }
#endif
}
