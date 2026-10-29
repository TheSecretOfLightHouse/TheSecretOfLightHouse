using System.Collections.Generic;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Harvest.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 test aid: buttons that give the pressing player resources, so the tower can be filled without harvesting.
    // The server adds to the sender's own inventory only, and only resources the workbench recipes use.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class DevResourceGrant : NetworkBehaviour
    {
        private const float Width = 300f;

        [Tooltip("Resources offered are the ones these recipes use.")]
        [SerializeField]
        private ModuleRecipeCatalog _catalog;
        [SerializeField, Min(1)]
        private int _amount = 10;

        private readonly List<ResourceDefinition> _resources = new List<ResourceDefinition>();
        private PlayerInventory _inventory;

        private void Awake()
        {
            CollectResources();
        }

        private void OnEnable()
        {
            DevPanelSwitch.Register();
        }

        private void OnDisable()
        {
            DevPanelSwitch.Unregister();
        }

        private void OnGUI()
        {
            if (!DevPanelSwitch.IsOn || !isActiveAndEnabled || !NetworkClient.active || netId == 0)
            {
                return;
            }

            if (!_inventory && NetworkClient.localPlayer)
            {
                _inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();
            }

            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, 10f, Width, 60f + _resources.Count * 24f), GUI.skin.box);
            GUILayout.Label($"개발자: 자원 받기 (+{_amount})");

            if (GUILayout.Button("전부 받기"))
            {
                CmdGrant(0);
            }

            for (int i = 0; i < _resources.Count; i++)
            {
                ResourceDefinition resource = _resources[i];
                int held = _inventory ? _inventory.GetSyncedCount(resource.Id) : 0;

                if (GUILayout.Button($"{resource.DisplayName} (보유 {held})"))
                {
                    CmdGrant(resource.Id);
                }
            }

            GUILayout.EndArea();
        }

        [Client]
        public void RequestGrantAll()
        {
            CmdGrant(0);
        }

        // 0 = every listed resource. The amount is the server's inspector value, not the client's.
        [Command(requiresAuthority = false)]
        private void CmdGrant(int resourceId, NetworkConnectionToClient sender = null)
        {
            if (sender == null || !sender.identity || !sender.identity.TryGetComponent(out PlayerInventory inventory))
            {
                return;
            }

            for (int i = 0; i < _resources.Count; i++)
            {
                if (resourceId == 0 || _resources[i].Id == resourceId)
                {
                    inventory.TryAdd(_resources[i].Id, _amount);
                }
            }
        }

        private void CollectResources()
        {
            _resources.Clear();

            for (int i = 0; _catalog && i < _catalog.Count; i++)
            {
                ModuleRecipe recipe = _catalog[i];

                for (int j = 0; recipe && j < recipe.IngredientCount; j++)
                {
                    ResourceDefinition resource = recipe.GetIngredient(j).Resource;

                    if (resource && !_resources.Contains(resource))
                    {
                        _resources.Add(resource);
                    }
                }
            }

            _resources.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
    }
}
