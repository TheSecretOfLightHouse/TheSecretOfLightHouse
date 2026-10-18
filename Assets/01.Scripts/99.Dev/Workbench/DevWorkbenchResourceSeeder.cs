#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Network.Session.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Workbench
{
    // JH03 test aid in the workbench test scene: gives every joining player a fixed stock on the server,
    // so deposits can be tried without harvesting. The amounts are what WorkbenchTestAutomation expects.
    [DisallowMultipleComponent]
    public sealed class DevWorkbenchResourceSeeder : MonoBehaviour
    {
        [SerializeField]
        private RecipeIngredient[] _grants = new RecipeIngredient[0];

        private LighthouseNetworkManager _manager;

        private void OnEnable()
        {
            _manager = LighthouseNetworkManager.Instance;

            if (_manager)
            {
                _manager.ActorJoined += HandleActorJoined;
            }
        }

        private void OnDisable()
        {
            if (_manager)
            {
                _manager.ActorJoined -= HandleActorJoined;
            }
        }

        private void HandleActorJoined(uint actorId, NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || connection == null || !connection.identity
                || !connection.identity.TryGetComponent(out PlayerInventory inventory))
            {
                return;
            }

            for (int i = 0; i < _grants.Length; i++)
            {
                inventory.TryAdd(_grants[i].ResourceId, _grants[i].Amount);
            }

            Debug.Log($"[{nameof(DevWorkbenchResourceSeeder)}] Seeded actor={actorId} kinds={_grants.Length}");
        }
    }
}
#endif
