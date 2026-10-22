using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Player.Boat;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerWreckRecovery : NetworkBehaviour
{
    [SerializeField, Min(1f)]
    private float _recoveryDuration = 90f;

    private PlayerController _player;
    private bool _isRecovering;
    private Vector3 _wreckPosition;
    private double _recoveryDeadline;

    private bool _isCompletingRecovery;
    private WreckRecoveryResult _recoveryResult;

    private PlayerBoatSpawner _boatSpawner;

    private Transform _boatSpawnPoint;

    private PlayerInventory _inventory;
    private PlayerInventoryDropper _dropper;
    private bool _hasSettledInventory;
    private uint _actorId;

    private InteractionTarget _rescueTarget;

    private static readonly List<PlayerWreckRecovery> ActiveRecoveries = new List<PlayerWreckRecovery>();

    private readonly List<KeyValuePair<int, int>> _plannedResources = new List<KeyValuePair<int, int>>();

    private bool _hasDropPlan;
    private int _plannedModuleRecipeId;
    private uint _plannedModuleId;

    private bool _hasDroppedResources;

    private WreckDropItem[] _resourceDrops = System.Array.Empty<WreckDropItem>();

    public static IReadOnlyList<PlayerWreckRecovery> Active => ActiveRecoveries;

    public PlayerController Player => _player;
    public InteractionTarget RescueTarget => _rescueTarget;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _inventory = GetComponent<PlayerInventory>();
        _dropper = GetComponent<PlayerInventoryDropper>();
        _rescueTarget = GetComponent<InteractionTarget>();
    }

    private void OnEnable()
    {
        ActiveRecoveries.Add(this);
        _player.DownedChanged += HandleDownedChanged;
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

    [ServerCallback]
    private void Update()
    {
        if (!_isRecovering || _isCompletingRecovery)
        {
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
        if (_isRecovering)
        {
            return false;
        }

        _isRecovering = true;
        _wreckPosition = wreckPosition;
        _recoveryDeadline = NetworkTime.time + _recoveryDuration;
        _hasSettledInventory = false;
        _hasDropPlan = false;
        _plannedResources.Clear();
        _plannedModuleRecipeId = 0;
        _plannedModuleId = 0;
        _hasDroppedResources = false;
        _resourceDrops = System.Array.Empty<WreckDropItem>();
        _player.SetDowned(true);

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
            || !wreckedBoat.IsOccupied || _player.CurrentBoat != wreckedBoat)
        {
            return false;
        }

        _isCompletingRecovery = true;
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

        if (!TryPrepareDropPlan())
        {
            return false;
        }

        if (!TryDropPlannedResources())
        {
            return false;
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
            || !wreckedBoat.IsOccupied || _player.CurrentBoat != wreckedBoat)
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

        if (!_dropper.TryDropResources(_plannedResources, _wreckPosition, out WreckDropItem[] droppedItems))
        {
            return false;
        }

        _resourceDrops = droppedItems;
        _hasDroppedResources = true;

        return true;
    }

    [Server]
    public bool TryCopyResourceDropsTo(List<WreckDropItem> results)
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();

        if (!isServer || !_hasDroppedResources)
        {
            return false;
        }

        for (int i = 0; i < _resourceDrops.Length; i++)
        {
            results.Add(_resourceDrops[i]);
        }

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
}