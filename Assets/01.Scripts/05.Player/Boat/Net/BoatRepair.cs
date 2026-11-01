using Mirror;
using System;
using UnityEngine;
using Lighthouse.Systems.Audio;

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

        public MaterialRequirement(int resourceId, int amount)
        {
            _resourceId = resourceId;
            _amount = amount;
        }
    }

    [Serializable]
    private sealed class RepairRecipe
    {
        [SerializeField]
        private string _displayName;

        [SerializeField, Min(0.01f)]
        private float _repairAmount;

        [SerializeField]
        private MaterialRequirement[] _requirements;

        public string DisplayName => _displayName;
        public float RepairAmount => _repairAmount;
        public MaterialRequirement[] Requirements => _requirements;

        public RepairRecipe(string displayName, float repairAmount, params MaterialRequirement[] requirements)
        {
            _displayName = displayName;
            _repairAmount = repairAmount;
            _requirements = requirements;
        }
    }

    [SerializeField]
    private RepairRecipe[] _recipes =
    {
        new RepairRecipe("간이 수리", 10f, new MaterialRequirement(2, 1)),
        new RepairRecipe("일반 수리", 30f, new MaterialRequirement(2, 2), new MaterialRequirement(3, 1)),
        new RepairRecipe("보강 수리", 50f, new MaterialRequirement(2, 3), new MaterialRequirement(4, 1), new MaterialRequirement(3, 1))
    };

    [SyncVar]
    private int _activeRecipeIndex = -1;

    private readonly SyncDictionary<int, int> _deposited = new SyncDictionary<int, int>();

    private BoatController _boat;
    private BoatDurability _durability;
    private bool _isConfigured;

    public int RecipeCount => _recipes == null ? 0 : _recipes.Length;
    public int ActiveRecipeIndex => _activeRecipeIndex;
    public bool HasPendingMaterials => _activeRecipeIndex >= 0;

    public string GetRecipeName(int recipeIndex)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        return recipe == null ? string.Empty : recipe.DisplayName;
    }

    public float GetRepairAmount(int recipeIndex)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        return recipe == null ? 0f : recipe.RepairAmount;
    }

    public int GetRequirementCount(int recipeIndex)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        return recipe == null || recipe.Requirements == null ? 0 : recipe.Requirements.Length;
    }

    public int GetRequirementResourceId(int recipeIndex, int index)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        return recipe != null && recipe.Requirements != null && index >= 0 && index < recipe.Requirements.Length
            && recipe.Requirements[index] != null ? recipe.Requirements[index].ResourceId : 0;
    }

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _durability = GetComponent<BoatDurability>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _deposited.Clear();
        _activeRecipeIndex = -1;
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

    public int GetRequiredAmount(int recipeIndex, int resourceId)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        if (recipe == null || recipe.Requirements == null)
        {
            return 0;
        }

        foreach (MaterialRequirement requirement in recipe.Requirements)
        {
            if (requirement != null && requirement.ResourceId == resourceId)
            {
                return requirement.Amount;
            }
        }

        return 0;
    }

    [Server]
    public bool TryRepair(PlayerController player, int recipeIndex = 0)
    {
        RepairRecipe recipe = GetRecipe(recipeIndex);
        if (!_isConfigured || !player || recipe == null
            || (HasPendingMaterials && _activeRecipeIndex != recipeIndex))
        {
            return false;
        }

        if (player.IsDowned || !player.IsOnBoat || player.CurrentBoat != _boat || !_boat.IsOccupied
            || (player.PassiveController && !player.PassiveController.IsJobAbilityEnabled))
        {
            return false;
        }

        float current = _durability.CurrentDurability;
        float maximum = _durability.MaxDurability;

        // Integer factors keep the inclusive 20% boundary free of 0.2f rounding.
        if (_durability.IsWrecked || maximum <= 0f || current * 5f < maximum || current >= maximum)
        {
            return false;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();

        if (!inventory)
        {
            return false;
        }

        bool hasDeposited = false;

        foreach (MaterialRequirement requirement in recipe.Requirements)
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
            _activeRecipeIndex = recipeIndex;
            hasDeposited = true;
        }

        if (!hasDeposited)
        {
            return false;
        }

        if (HasAllMaterials(recipe))
        {
            float repairMultiplier = 1f;

            if (player.TryGetComponent(out PlayerPassiveController passiveController))
            {
                repairMultiplier = passiveController.GetRepairAmountMultiplier(_boat);
            }

            float repairAmount = recipe.RepairAmount * repairMultiplier;

            _durability.RestoreDurability(repairAmount);
            if (_durability.CurrentDurability > current)
            {
                NotifyRepaired();
            }
            _deposited.Clear();
            _activeRecipeIndex = -1;
        }

        return true;
    }

    [Server]
    public void NotifyRepaired()
    {
        RpcPlayRepairSound();
    }

    [ClientRpc]
    private void RpcPlayRepairSound()
    {
        if (SoundManager.Instance)
        {
            SoundManager.Instance.PlaySfxAt(SfxType.BoatRepair, transform.position);
        }
    }

    private RepairRecipe GetRecipe(int recipeIndex)
    {
        return _recipes != null && recipeIndex >= 0 && recipeIndex < _recipes.Length ? _recipes[recipeIndex] : null;
    }

    private bool HasAllMaterials(RepairRecipe recipe)
    {
        foreach (MaterialRequirement requirement in recipe.Requirements)
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
        _activeRecipeIndex = -1;
    }

    private bool ValidateConfiguration()
    {
        if (_recipes == null || _recipes.Length == 0)
        {
            return false;
        }

        foreach (RepairRecipe recipe in _recipes)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.DisplayName)
                || recipe.Requirements == null || recipe.Requirements.Length == 0
                || recipe.RepairAmount <= 0f || float.IsNaN(recipe.RepairAmount) || float.IsInfinity(recipe.RepairAmount))
            {
                return false;
            }

            for (int i = 0; i < recipe.Requirements.Length; i++)
            {
                MaterialRequirement requirement = recipe.Requirements[i];
                if (requirement == null || requirement.ResourceId <= 0 || requirement.Amount <= 0)
                {
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (recipe.Requirements[j].ResourceId == requirement.ResourceId)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
