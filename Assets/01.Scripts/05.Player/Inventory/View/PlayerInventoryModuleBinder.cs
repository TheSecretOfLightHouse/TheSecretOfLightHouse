using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Workbench.Net;
using Mirror;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryModuleBinder : MonoBehaviour
{
    [SerializeField]
    private WorkbenchNet _workbench;

    private Coroutine _bindRoutine;
    private NetworkIdentity _boundPlayer;
    private PlayerInventory _inventory;

    private void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindLocalInventory());
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        if (_inventory)
        {
            _inventory.BindModuleSource(null);
        }

        _inventory = null; 
        _boundPlayer = null;
    }

    private IEnumerator BindLocalInventory()
    {
        while (true)
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;

            if (localPlayer != _boundPlayer)
            {
                if (_inventory)
                {
                    _inventory.BindModuleSource(null);
                }

                _boundPlayer = localPlayer;
                _inventory = null;

                if (localPlayer && localPlayer.TryGetComponent(out PlayerInventory inventory))
                {
                    _inventory = inventory;
                    _inventory.BindModuleSource(_workbench);
                }
            }

            yield return null;
        }
    }

    [ContextMenu("Test/Deposit Searchlight Ingredients")]
    private void TestDepositIngredients()
    {
        if (!_inventory || !_workbench || !NetworkClient.ready)
        {
            return;
        }

        StartCoroutine(DepositSearchlightIngredients());
    }

    private IEnumerator DepositSearchlightIngredients()
    {
        if (!_workbench.Catalog || !_workbench.Catalog.TryGetById(101, out Lighthouse.Workbench.Rules.ModuleRecipe recipe))
        {
            yield break;
        }

        InteractionRelay relay = _inventory.GetComponent<InteractionRelay>();
        if (!relay)
        {
            yield break;
        }

        for (int i = 0; i < recipe.IngredientCount; i++)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5.0;
            while (relay && relay.IsAwaitingResult && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!relay || !_workbench || !NetworkClient.ready || relay.IsAwaitingResult)
            {
                yield break;
            }

            int resourceId = recipe.GetIngredient(i).ResourceId;
            int remaining = _workbench.GetRemaining(recipe.Id, resourceId);
            if (remaining > 0 && !relay.TrySendToNetworkTarget(_workbench.netId,
                InteractionAction.Deposit, recipe.Id, resourceId, remaining))
            {
                yield break;
            }
        }
    }

    [ContextMenu("Test/Craft Searchlight")]
    private void TestCraftModule()
    {
        SendWorkbenchRequest(InteractionAction.Craft, 101);
    }

    private void SendWorkbenchRequest(InteractionAction action, int recipeId, int resourceId = 0, int amount = 0)
    {
        if (!_inventory || !_workbench || _workbench.netId == 0 || !NetworkClient.ready)
        {
            return;
        }

        InteractionRelay relay = _inventory.GetComponent<InteractionRelay>();

        if (!relay)
        {
            return;
        }

        bool sent = relay.TrySendToNetworkTarget(_workbench.netId, action, recipeId, resourceId, amount);
    }

    [ContextMenu("Test/Take Searchlight Module")]
    private void TestTakeModule()
    {
        if (!_inventory || !_workbench)
        {
            return;
        }

        InteractionRelay relay = _inventory.GetComponent<InteractionRelay>();

        if (!relay || _workbench.netId == 0)
        {
            return;
        }

        bool sent = relay.TrySendToNetworkTarget(_workbench.netId, InteractionAction.TakeModule, 101);
    }

    [ContextMenu("Test/Read Inventory Module")]
    private void TestReadModule()
    {
        if (!_inventory)
        {
            return;
        }

        Debug.Log(
            $"[{nameof(PlayerInventoryModuleBinder)}] " +
            $"HasModule={_inventory.HasModule}, " +
            $"RecipeId={_inventory.ModuleRecipeId}",
            this);
    }

    [ContextMenu("Test/Install Carried Module On Boat")]
    private void TestInstallModule()
    {
        if (!_inventory || !_inventory.HasModule)
        {
            return;
        }

        PlayerController player = _inventory.GetComponent<PlayerController>();

        if (!player || player.IsDowned || !player.IsOnBoat || !player.CurrentBoat)
        {
            return;
        }

        InteractionRelay relay = _inventory.GetComponent<InteractionRelay>();

        if (!relay || player.CurrentBoat.netId == 0)
        {
            return;
        }

        bool sent = relay.TrySendToNetworkTarget(player.CurrentBoat.netId, InteractionAction.InstallModule, _inventory.ModuleRecipeId);

        Debug.Log(
            $"[{nameof(PlayerInventoryModuleBinder)}] " +
            $"Install request sent: {sent}",
            this);
    }

    [ContextMenu("Test/Read Boat Modules")]
    private void TestReadBoatModules()
    {
        if (!_inventory)
        {
            return;
        }

        PlayerController player = _inventory.GetComponent<PlayerController>();

        if (!player || !player.CurrentBoat)
        {
            return;
        }

        BoatModuleEquipment equipment = player.CurrentBoat.GetComponent<BoatModuleEquipment>();

        if (!equipment)
        {
            return;
        }

        Debug.Log(
            $"[{nameof(PlayerInventoryModuleBinder)}] " +
            $"SearchlightInstalled=" +
            $"{equipment.HasModule(BoatModuleType.Searchlight)}, " +
            $"InventoryHasModule={_inventory.HasModule}",
            this);
    }
}
