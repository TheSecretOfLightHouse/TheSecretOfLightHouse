using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Workbench
{
    // JH03 test GUI kept beside the Canvas WorkbenchScreen: view a recipe (local only), deposit from the local player's
    // inventory, and watch the shared progress of that recipe. Requests go through the local player's relay like real input would.
    [DisallowMultipleComponent]
    public sealed class WorkbenchTestPanel : MonoBehaviour
    {
        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private Rect _area = new Rect(10f, 280f, 360f, 420f);

        private InteractionRelay _relay;
        private PlayerInventory _inventory;
        private string _lastResult = "-";
        private ModuleRecipe _viewed;

        private void OnEnable()
        {
            DevPanelSwitch.Register();
        }

        private void OnDisable()
        {
            DevPanelSwitch.Unregister();

            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private void OnGUI()
        {
            // In a build, Mirror keeps scene objects inactive on a client until spawned, and netId is not readable before.
            if (!DevPanelSwitch.IsOn || !_workbench || !_workbench.isActiveAndEnabled || !NetworkClient.active || !_workbench.Catalog)
            {
                return;
            }

            BindLocalPlayer();
            GUILayout.BeginArea(_area, GUI.skin.box);
            GUILayout.Label($"작업대 (시험) netId={_workbench.netId}  마지막 결과: {_lastResult}");
            DrawRecipes();
            DrawProgress();
            DrawStock();
            GUILayout.EndArea();
        }

        private void BindLocalPlayer()
        {
            if (_relay || !NetworkClient.localPlayer)
            {
                return;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();
            _inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();

            if (_relay)
            {
                _relay.ResultReceived += HandleResultReceived;
            }
        }

        private void DrawRecipes()
        {
            ModuleRecipeCatalog catalog = _workbench.Catalog;
            GUILayout.Label("레시피 보기");

            for (int i = 0; i < catalog.Count; i++)
            {
                ModuleRecipe recipe = catalog[i];

                string mark = recipe == _viewed ? "▶ " : string.Empty;

                if (GUILayout.Button($"{mark}{recipe.Id}. {recipe.DisplayName}{(recipe.IsTestValue ? " (시험값)" : string.Empty)}"))
                {
                    _viewed = recipe;
                }
            }
        }

        private void DrawProgress()
        {
            ModuleRecipe recipe = _viewed;

            if (!recipe)
            {
                GUILayout.Label("레시피를 고르세요");
                return;
            }

            GUILayout.Label($"보는 중: {recipe.DisplayName}");

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);
                int held = _inventory ? _inventory.GetSyncedCount(ingredient.ResourceId) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{ingredient.Resource.DisplayName} {_workbench.GetDepositedAt(recipe.Id, i)}/{ingredient.Amount} (보유 {held})");

                if (GUILayout.Button("+1", GUILayout.Width(40f)))
                {
                    Send(InteractionAction.Deposit, recipe.Id, ingredient.ResourceId, 1);
                }

                if (GUILayout.Button("전부", GUILayout.Width(50f)))
                {
                    Send(InteractionAction.Deposit, recipe.Id, ingredient.ResourceId, ingredient.Amount);
                }

                GUILayout.EndHorizontal();
            }

            GUI.enabled = _workbench.IsReadyToCraft(recipe.Id);

            if (GUILayout.Button("제작"))
            {
                Send(InteractionAction.Craft, recipe.Id, 0, 0);
            }

            GUI.enabled = true;
        }

        private void DrawStock()
        {
            ModuleRecipeCatalog catalog = _workbench.Catalog;
            GUILayout.Label("보관 중 모듈");

            for (int i = 0; i < catalog.Count; i++)
            {
                int count = _workbench.GetStoredCount(catalog[i].Id);

                if (count > 0)
                {
                    GUILayout.Label($"{catalog[i].DisplayName} x{count}");
                }
            }
        }

        private void Send(InteractionAction action, int arg0, int arg1, int arg2)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1, arg2))
            {
                _lastResult = "보내지 못함";
            }
        }

        private void HandleResultReceived(InteractionResult result)
        {
            _lastResult = result.Code.ToString();
        }
    }
}
