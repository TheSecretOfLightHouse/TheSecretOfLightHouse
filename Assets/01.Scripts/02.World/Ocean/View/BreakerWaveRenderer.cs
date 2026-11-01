using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Ocean.View
{
    public sealed class BreakerWaveRenderer : IDisposable
    {
        private const int Columns = 32;
        private const int Rows = 20;
        private const int Parts = 3;
        private static readonly int BufferA = Shader.PropertyToID("_BreakerA");
        private static readonly int BufferB = Shader.PropertyToID("_BreakerB");
        private static readonly int CountId = Shader.PropertyToID("_BreakerCount");
        private static readonly int SeaId = Shader.PropertyToID("_BreakerSea");
        private static readonly int TimesId = Shader.PropertyToID("_BreakerTimes");
        private static readonly int SpillingId = Shader.PropertyToID("_BreakerSpilling");
        private static readonly int PlungingId = Shader.PropertyToID("_BreakerPlunging");
        private readonly Material _material;
        private readonly BreakerWaveTuning _tuning = BreakerWaveTuning.Default;
        private readonly BreakerSourceCollector _collector;
        private readonly BreakerEvent[] _events = new BreakerEvent[BreakerSourceCollector.Capacity];
        private readonly Vector4[] _a = new Vector4[BreakerSourceCollector.Capacity];
        private readonly Vector4[] _b = new Vector4[BreakerSourceCollector.Capacity];

        public int SourceCount => _collector.Count;
        public int CandidateCount => _collector.CandidateCount;
        public int ActiveCount { get; private set; }

        public BreakerWaveRenderer(Shader shader)
        {
            _collector = new BreakerSourceCollector(in _tuning);
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            Shader.SetGlobalVector(TimesId, new Vector4(_tuning.ApproachEnd, _tuning.CurlEnd, _tuning.ImpactEnd, _tuning.Duration));
            BreakerShape spilling = BreakerShape.FromSteepness(0f);
            BreakerShape plunging = BreakerShape.FromSteepness(1f);
            Shader.SetGlobalVector(SpillingId, ToVector(in spilling));
            Shader.SetGlobalVector(PlungingId, ToVector(in plunging));
        }

        public void Update(Vector3 focus, float seaLevel, WaterSamplerHost host, double time, int layer)
        {
            ActiveCount = 0;
            if (F3WaveField.IsActive || host == null || !BreakerSwell.TryGet(host.Inputs.Waves, out Vector2 swell, out float energy))
            {
                return;
            }

            _collector.Refresh(focus, seaLevel, swell, Time.unscaledTime);
            ActiveCount = BreakerEventSchedule.Collect(in _tuning, _collector.Sources, energy, time, _events);
            if (ActiveCount == 0)
            {
                return;
            }

            for (int i = 0; i < ActiveCount; i++)
            {
                BreakerEvent e = _events[i];
                _a[i] = new Vector4(e.ImpactPoint.x, e.ImpactPoint.y, e.Direction.x, e.Direction.y);
                _b[i] = new Vector4(e.Width, e.Height, e.Age, e.Strength);
            }

            Shader.SetGlobalVectorArray(BufferA, _a);
            Shader.SetGlobalVectorArray(BufferB, _b);
            Shader.SetGlobalFloat(CountId, ActiveCount);
            Shader.SetGlobalFloat(SeaId, seaLevel);
            Graphics.DrawProcedural(_material, new Bounds(focus, new Vector3(100f, 40f, 100f)),
                MeshTopology.Triangles, Columns * Rows * 6, ActiveCount * Parts, null, null,
                ShadowCastingMode.Off, false, layer);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(_material);
        }

        private static Vector4 ToVector(in BreakerShape shape)
        {
            return new Vector4(shape.Curl, shape.LipLength, shape.SheetHeight, shape.FoamDensity);
        }
    }
}
