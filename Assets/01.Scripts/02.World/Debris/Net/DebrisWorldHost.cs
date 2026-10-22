using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Debris.Server;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Debris.Net
{
    [DisallowMultipleComponent]
    public class DebrisWorldHost : MonoBehaviour
    {
        [SerializeField] private MapStateNet _map;
        [SerializeField] private DebrisConfig _config;

        private ShoreDropLocator _locator;
        private DebrisLifetimeTracker _tracker;

        private void Awake()
        {
            if (_map == null || _config == null)
            {
                Debug.LogError($"[{nameof(DebrisWorldHost)}] MapStateNet and DebrisConfig are required.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _map.LayoutReady += HandleLayoutReady;
            _map.LayoutReleased += HandleLayoutReleased;

            if (_map.IsLayoutReady && _map.Layout != null)
            {
                HandleLayoutReady(_map.Layout, _map.Info);
            }
        }

        private void OnDisable()
        {
            _map.LayoutReady -= HandleLayoutReady;
            _map.LayoutReleased -= HandleLayoutReleased;
            Release();
        }

        private void Update()
        {
            if (_tracker != null)
            {
                _tracker.Tick();
            }
        }

        private void HandleLayoutReady(MapLayout layout, MapSeedInfo info)
        {
            Release();

            if (!NetworkServer.active)
            {
                return;
            }

            if (!WorldServices.TryGet(out IWorldQuery world)
                || !WorldServices.TryGet(out IRegionGate regions)
                || !WorldServices.TryGet(out IClock clock)
                || !WorldServices.TryGet(out IGroundHeightSource ground))
            {
                Debug.LogError($"[{nameof(DebrisWorldHost)}] Required world services are unavailable.");
                return;
            }

            _locator = new ShoreDropLocator(layout.Islands, ground, world, regions, _config);
            _tracker = new DebrisLifetimeTracker(clock, _config);
            WorldServices.Register<IShoreDropLocator>(_locator);
            WorldServices.Register<IDebrisTracker>(_tracker);
        }

        private void HandleLayoutReleased()
        {
            Release();
        }

        private void Release()
        {
            if (_locator != null)
            {
                WorldServices.Unregister<IShoreDropLocator>(_locator);
                _locator = null;
            }

            if (_tracker != null)
            {
                WorldServices.Unregister<IDebrisTracker>(_tracker);
                _tracker.Dispose();
                _tracker = null;
            }
        }
    }
}
