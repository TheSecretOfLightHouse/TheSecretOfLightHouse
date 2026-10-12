using System.Collections.Generic;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [DefaultExecutionOrder(-100)]
    public class WaterSamplerHost : MonoBehaviour
    {
        [SerializeField] private int _shipCapacity = 32;
        [SerializeField] private int _driftCapacity = 90;

        [SerializeField]
        private WaveParams[] _initialWaves =
        {
            new WaveParams { Amplitude = 0.5f, Wavelength = 20f, Direction = new Vector2(1f, 0f), Steepness = 0.3f, Speed = 3f },
            new WaveParams { Amplitude = 0.25f, Wavelength = 9f, Direction = new Vector2(0.6f, 0.8f), Steepness = 0.2f, Speed = 2.2f }
        };

        private readonly List<IWaterSampleSource> _sources = new List<IWaterSampleSource>();
        private readonly WaveInputs _inputs = new WaveInputs();
        private WaterSampler _sampler;
        private IClock _clock;

        public WaveInputs Inputs => _inputs;

        public double CurrentTime { get; private set; }

        private void Awake()
        {
            if (!_inputs.TrySetWaves(_initialWaves))
            {
                Debug.LogError($"[{nameof(WaterSamplerHost)}] Initial waves are invalid.");
                enabled = false;
                return;
            }

            _sampler = new WaterSampler(_shipCapacity, _driftCapacity);
            WorldServices.Register(this);
        }

        private void Start()
        {
            if (!WorldServices.TryGet<IClock>(out _clock))
            {
                Debug.LogError($"[{nameof(WaterSamplerHost)}] IClock is not registered. Add a GameClock to the scene.");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            WorldServices.Unregister(this);

            if (_sampler != null)
            {
                _sampler.Dispose();
            }
        }

        private void FixedUpdate()
        {
            if (_clock == null)
            {
                return;
            }

            CurrentTime = _clock.Now;

            for (int i = 0; i < _sources.Count; i++)
            {
                _sources[i].SubmitPoints();
            }

            _sampler.Run(_inputs.Waves, _inputs.Modifiers, (float)CurrentTime);
        }

        public void RegisterSource(IWaterSampleSource source)
        {
            if (source != null && !_sources.Contains(source))
            {
                _sources.Add(source);
            }
        }

        public void UnregisterSource(IWaterSampleSource source)
        {
            _sources.Remove(source);
        }

        public bool TryAllocate(SamplerPool pool, int count, out SamplerTicket ticket)
        {
            if (_sampler == null)
            {
                ticket = default;
                return false;
            }

            return _sampler.TryAllocate(pool, count, out ticket);
        }

        public void Release(in SamplerTicket ticket)
        {
            if (_sampler != null)
            {
                _sampler.Release(in ticket);
            }
        }

        public void SetPoint(in SamplerTicket ticket, int index, Vector3 worldPos)
        {
            if (_sampler != null)
            {
                _sampler.SetPoint(in ticket, index, worldPos);
            }
        }

        public float GetHeight(in SamplerTicket ticket, int index)
        {
            return _sampler != null ? _sampler.GetHeight(in ticket, index) : 0f;
        }
    }
}
