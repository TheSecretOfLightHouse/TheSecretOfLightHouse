using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1002)]
    public sealed class WaterContactEffectsView : MonoBehaviour
    {
        private static WaterContactEffectsView _active;
        [SerializeField] private Transform _focus;
        [SerializeField] private ComputeShader _simulation;
        [SerializeField] private Texture2D _foamDetail;
        [SerializeField] private Shader _sprayShader;
        [SerializeField] private Shader _crestShader;
        private IWorldQuery _query;
        private IClock _clock;
        private WaterSamplerHost _host;
        private SurfaceInteractionGrid _grid;
        private WaterSprayRenderer _spray;

        public int TrackedContactCount => _grid != null ? _grid.ObstacleCount : 0;
        public int SimulationStepCount => _grid != null ? _grid.StepCount : 0;
        public int UnsupportedMeshCount => _grid != null ? _grid.UnsupportedMeshCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwner()
        {
            _active = null;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            if (_active != null && _active != this)
            {
                Debug.LogError($"[{nameof(WaterContactEffectsView)}] Another interaction grid is active.");
                enabled = false;
                return;
            }
            if (_focus == null || _simulation == null || _foamDetail == null || _sprayShader == null
                || !_sprayShader.isSupported || _crestShader == null || !_crestShader.isSupported || !SystemInfo.supportsComputeShaders
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
            {
                Debug.LogError($"[{nameof(WaterContactEffectsView)}] Focus, simulation, foam texture and compute support are required.");
                enabled = false;
                return;
            }
            _active = this;
            Shader.SetGlobalTexture("_OceanFoamDetail", _foamDetail);
            _grid = new SurfaceInteractionGrid(_simulation);
            _spray = new WaterSprayRenderer(_grid, _sprayShader, _crestShader);
            _query = null;
            _clock = null;
            _host = null;
        }

        private void LateUpdate()
        {
            if (_grid == null || _focus == null)
            {
                return;
            }
            if (_query == null && !WorldServices.TryGet(out _query))
            {
                return;
            }
            if (_clock == null && !WorldServices.TryGet(out _clock))
            {
                return;
            }
            if (_host == null && !WorldServices.TryGet(out _host))
            {
                return;
            }
            float seaLevel = _query.SampleWaterHeight(_focus.position);
            _grid.Update(_focus.position, seaLevel, Time.deltaTime, Time.time, _host, (float)_clock.Now);
            _spray.Update(_focus.position, Time.deltaTime, gameObject.layer);
        }

        private void OnDisable()
        {
            if (_spray != null)
            {
                _spray.Dispose();
                _spray = null;
            }
            if (_grid != null)
            {
                _grid.Dispose();
                _grid = null;
            }
            if (_active == this)
            {
                _active = null;
            }
        }
    }
}
