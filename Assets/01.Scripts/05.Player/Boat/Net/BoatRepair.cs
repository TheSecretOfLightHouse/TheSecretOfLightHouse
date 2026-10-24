using Mirror;
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(BoatDurability))]
public sealed class BoatRepair : NetworkBehaviour
{
    [Serializable]
    private sealed class MaterialRequirement
    {
        [SerializeField]
        private int _resourceId;

        [SerializeField, Min(1)]
        private int _amount = 1;

        public int ResourceId => _resourceId;
        public int Amount => _amount;
    }

    [SerializeField]
    private MaterialRequirement[] _requirements;

    [SerializeField, Min(0.01f)]
    private float _repairAmount = 30f;

    private readonly SyncDictionary<int, int> _deposited = new SyncDictionary<int, int>();

    private BoatController _boat;
    private BoatDurability _durability;
    private bool _isConfigured;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _durability = GetComponent<BoatDurability>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _deposited.Clear();
        _isConfigured = ValidateConfiguration();
        _durability.Wrecked += HandleWrecked;

        if (!_isConfigured)
        {
            return;
        }
    }

    public override void OnStopServer()
    {
        if (_durability)
        {
            _durability.Wrecked -= HandleWrecked;
        }

        base.OnStopServer();
    }

    public int GetDepositedAmount(int resourceId)
    {
        return _deposited.TryGetValue(resourceId, out int amount) ? amount : 0;
    }

    public int GetRequiredAmount(int resourceId)
    {
        if (_requirements == null)
        {
            return 0;
        }

        foreach (MaterialRequirement requirement in _requirements)
        {
            if (requirement != null && requirement.ResourceId == resourceId)
            {
                return requirement.Amount;
            }
        }

        return 0;
    }

    [Server]
    public bool TryRepair(PlayerController player)
    {
        if (!_isConfigured || !player)
        {
            return false;
        }

        if (player.IsDowned || !player.IsOnBoat || player.CurrentBoat != _boat || !_boat.IsOccupied)
        {
            return false;
        }

        float current = _durability.CurrentDurability;
        float maximum = _durability.MaxDurability;

        if (_durability.IsWrecked || current < maximum * 0.3f || current >= maximum)
        {
            return false;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();

        if (!inventory)
        {
            return false;
        }

        bool hasDeposited = false;

        foreach (MaterialRequirement requirement in _requirements)
        {
            int deposited = GetDepositedAmount(requirement.ResourceId);

            int remaining = requirement.Amount - deposited;
            if (remaining <= 0)
            {
                continue;
            }

            int removed = inventory.TryRemoveTo(requirement.ResourceId, remaining);

            if (removed <= 0)
            {
                continue;
            }

            _deposited[requirement.ResourceId] = deposited + removed;
            hasDeposited = true;
        }

        if (!hasDeposited)
        {
            return false;
        }

        if (HasAllMaterials())
        {
            float repairMultiplier = 1f;

            if (player.TryGetComponent(out PlayerPassiveController passiveController))
            {
                repairMultiplier = passiveController.GetRepairAmountMultiplier(_boat);
            }

            float repairAmount = _repairAmount * repairMultiplier;

            Debug.Log($"{repairAmount}");
            _durability.RestoreDurability(repairAmount);
            _deposited.Clear();
        }

        return true;
    }

    private bool HasAllMaterials()
    {
        foreach (MaterialRequirement requirement in _requirements)
        {
            if (GetDepositedAmount(requirement.ResourceId) < requirement.Amount)
            {
                return false;
            }
        }

        return true;
    }

    [Server]
    private void HandleWrecked()
    {
        _deposited.Clear();
    }

    private bool ValidateConfiguration()
    {
        if (_requirements == null || _requirements.Length == 0
            || _repairAmount <= 0f
            || float.IsNaN(_repairAmount)
            || float.IsInfinity(_repairAmount))
        {
            return false;
        }

        for (int i = 0; i < _requirements.Length; i++)
        {
            MaterialRequirement requirement = _requirements[i];

            if (requirement == null || requirement.Amount <= 0)
            {
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (_requirements[j].ResourceId == requirement.ResourceId)
                {
                    return false;
                }
            }
        }

        return true;
    }
}