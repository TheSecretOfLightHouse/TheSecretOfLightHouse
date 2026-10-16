using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Server;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Harvest.Server
{
    [DefaultExecutionOrder(-800)]
    public class HarvestHost : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _serverRange = 3f;

        private HarvestSpotRegistry _registry;
        private HarvestTargetAdapter _adapter;
        private HarvestRegenDriver _driver;

        public HarvestSpotRegistry Registry => _registry;

        private void Awake()
        {
            if (NetworkClient.active && !NetworkServer.active)
            {
                enabled = false;
                return;
            }

            _registry = new HarvestSpotRegistry();
            _adapter = new HarvestTargetAdapter(_registry, _serverRange);
            WorldServices.Register<IHarvestSpotRegistry>(_registry);
            WorldServices.Register<IHarvestSpotQuery>(_registry);
            WorldServices.Register<IHarvestIslandRegistrar>(_registry);
            WorldServices.Register<IHarvestChangeSource>(_registry);
            TargetResolver.RegisterAdapter(InteractionTargetKind.HarvestSpot, _adapter);
        }

        private void Start()
        {
            if (_registry == null)
            {
                return;
            }

            if (!WorldServices.TryGet<IGameClock>(out IGameClock clock))
            {
                Debug.LogError($"[{nameof(HarvestHost)}] IGameClock is not registered. Add a GameClock to the scene.");
                return;
            }

            _driver = new HarvestRegenDriver(clock, _registry, new ProportionalRegenPolicy());
        }

        private void OnDestroy()
        {
            if (_driver != null)
            {
                _driver.Dispose();
            }

            if (_registry == null)
            {
                return;
            }

            WorldServices.Unregister<IHarvestSpotRegistry>(_registry);
            WorldServices.Unregister<IHarvestSpotQuery>(_registry);
            WorldServices.Unregister<IHarvestIslandRegistrar>(_registry);
            WorldServices.Unregister<IHarvestChangeSource>(_registry);
            TargetResolver.UnregisterAdapter(InteractionTargetKind.HarvestSpot, _adapter);
        }
    }
}
