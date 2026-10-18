#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Workbench
{
    // JH03 test GUI until the real workbench UI (MK06): pick a recipe, deposit from the local player's inventory,
    // and watch the shared progress. Requests go through the local player's relay like real input would.
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

        private void OnDisable()
        {
            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private void OnGUI()
        {
            // In a build, Mirror keeps scene objects inactive on a client until spawned, and netId is not readable before.
            if (!_workbench || !_workbench.isActiveAndEnabled || !NetworkClient.active || !_workbench.Catalog)
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
            GUILayout.Label("레시피 선택");

            for (int i = 0; i < catalog.Count; i++)
            {
                ModuleRecipe recipe = catalog[i];

                if (GUILayout.Button($"{recipe.Id}. {recipe.DisplayName}{(recipe.IsTestValue ? " (시험값)" : string.Empty)}"))
                {
                    Send(InteractionAction.SelectRecipe, recipe.Id, 0);
                }
            }
        }

        private void DrawProgress()
        {
            if (!_workbench.TryGetCurrentRecipe(out ModuleRecipe recipe))
            {
                GUILayout.Label("진행 중 레시피 없음");
                return;
            }

            GUILayout.Label($"진행 중: {recipe.DisplayName}");

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                RecipeIngredient ingredient = recipe.GetIngredient(i);
                int held = _inventory ? _inventory.GetSyncedCount(ingredient.ResourceId) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{ingredient.Resource.DisplayName} {_workbench.GetDepositedAt(i)}/{ingredient.Amount} (보유 {held})");

                if (GUILayout.Button("+1", GUILayout.Width(40f)))
                {
                    Send(InteractionAction.Deposit, ingredient.ResourceId, 1);
                }

                if (GUILayout.Button("전부", GUILayout.Width(50f)))
                {
                    Send(InteractionAction.Deposit, ingredient.ResourceId, ingredient.Amount);
                }

                GUILayout.EndHorizontal();
            }
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

        private void Send(InteractionAction action, int arg0, int arg1)
        {
            if (!_relay || !_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1))
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
#endif
