using Lighthouse.World.Clock.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.View;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Lighthouse.Dev.Cinematics
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SpecialEndingOceanPreview : MonoBehaviour, IClock
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private WaterSamplerHost _host;
        [SerializeField] private WeatherCatalog _weatherCatalog;
        [SerializeField] private OceanOpticsProfile _opticsProfile;
        [SerializeField] private MapGenerationConfig _mapConfig;
        [SerializeField] private Material _sourceMaterial;
        [SerializeField] private Renderer[] _renderers = new Renderer[0];
        private OceanOpticsMaterial _material;
        private bool _ownsClock;

        public double Now => _director != null ? _director.time : Time.timeAsDouble;

        private void Awake()
        {
            if (NetworkClient.active || NetworkServer.active || SceneManager.sceneCount != 1
                || WorldServices.TryGet<IClock>(out _))
            {
                enabled = false;
                return;
            }
            WorldServices.Register<IClock>(this);
            _ownsClock = true;
        }

        private void Start()
        {
            if (!_ownsClock || _host == null || _weatherCatalog == null || _opticsProfile == null
                || !_weatherCatalog.TryGetProfile(WeatherType.Maelstrom, out WeatherProfile weather)
                || !WeatherSnapshot.TryCreate(_weatherCatalog.Layout, weather, out WeatherSnapshot snapshot)
                || !_host.Inputs.TrySetWaves(snapshot.Waves)
                || !OceanOpticsMaterial.TryCreate(_renderers, _sourceMaterial, out _material))
            {
                Debug.LogError($"[{nameof(SpecialEndingOceanPreview)}] Main ocean assets are required.", this);
                enabled = false;
                return;
            }
            // Use the same appearance model and abyss profile as the summoned main-game ocean.
            OceanOpticsState state = OceanOpticsModel.Evaluate(_opticsProfile, 0f, 1f, 1f);
            _material.Apply(in state);
            if (_mapConfig != null && _mapConfig.Zones.Count == 3)
            {
                _material.ApplyZones(_opticsProfile,
                    _mapConfig.Zones[0].OuterRadius * _mapConfig.RadiusScale,
                    _mapConfig.Zones[1].OuterRadius * _mapConfig.RadiusScale,
                    _mapConfig.Zones[2].OuterRadius * _mapConfig.RadiusScale);
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                _material.Dispose();
                _material = null;
            }
            if (_ownsClock)
            {
                WorldServices.Unregister<IClock>(this);
            }
        }
    }
}
