using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    [DisallowMultipleComponent]
    public sealed class WeatherRainView : MonoBehaviour
    {
        private const int GridWidth = 5;
        private const int CellCount = GridWidth * GridWidth;
        [SerializeField] private Material _dropMaterial;
        [SerializeField] private Material _splashMaterial;
        [SerializeField] private AudioClip _rainClip;
        [SerializeField, Min(1)] private int _maxDrops = 1800;
        [SerializeField, Min(1f)] private float _radius = 18f;
        [SerializeField, Min(1f)] private float _height = 13f;
        [SerializeField, Min(1f)] private float _fallSpeed = 22f;
        [SerializeField, Min(0.02f)] private float _probeInterval = 0.15f;
        [SerializeField, Min(0f)] private float _dropsPerSecond = 1100f;
        [SerializeField, Range(0f, 1f)] private float _rainVolume = 0.24f;
        [SerializeField] private LayerMask _coverMask = ~0;

        private readonly float[] _surface = new float[CellCount];
        private readonly bool[] _covered = new bool[CellCount];
        private ParticleSystem _drops;
        private ParticleSystem _splashes;
        private AudioSource _audio;
        private AudioClip _generatedClip;
        private float _nextProbe;
        private float _budget;
        private Vector3 _lastPosition;
        private bool _hasPosition;
        private int _cell;
        private uint _random = 0x7431u;
        private bool _hasWorldAudio;

        public float AudioExposure => _covered[CellCount / 2] ? 0.2f : 1f;

        private void OnEnable()
        {
            _drops = CreateParticles("WeatherRain", _dropMaterial, _maxDrops, false);
            _splashes = CreateParticles("WeatherRainSplashes", _splashMaterial, 160, true);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0f;
            _hasWorldAudio = Resources.Load<WorldAudioProfile>(WorldAudioProfile.ResourceName) != null;
            if (_rainClip == null && !_hasWorldAudio)
            {
                _generatedClip = CreateRainClip();
            }
            _audio.clip = _rainClip != null ? _rainClip : _generatedClip;
        }

        private void OnDisable()
        {
            Clear();
            if (_drops != null)
            {
                Destroy(_drops.gameObject);
            }
            if (_splashes != null)
            {
                Destroy(_splashes.gameObject);
            }
            if (_audio != null)
            {
                Destroy(_audio);
            }
            if (_generatedClip != null)
            {
                Destroy(_generatedClip);
            }
        }

        public void RenderWeather(Camera observer, in WeatherSnapshot snapshot)
        {
            if (!isActiveAndEnabled || observer == null || _drops == null)
            {
                Clear();
                return;
            }

            Vector3 position = observer.transform.position;
            if (!_hasPosition || (position - _lastPosition).sqrMagnitude > _radius * _radius)
            {
                Clear();
                _nextProbe = 0f;
            }
            _lastPosition = position;
            _hasPosition = true;
            float rain = Mathf.Clamp01(snapshot.Rain);
            if (Time.unscaledTime >= _nextProbe)
            {
                Probe(position);
                _nextProbe = Time.unscaledTime + _probeInterval;
            }

            bool isCovered = _covered[CellCount / 2];
            float volume = _hasWorldAudio ? 0f : rain * _rainVolume * (isCovered ? 0.2f : 1f);
            _audio.volume = Mathf.MoveTowards(_audio.volume, volume, Time.deltaTime);
            if (!_hasWorldAudio && rain > 0f && !_audio.isPlaying)
            {
                _audio.Play();
            }
            if (rain <= 0f && _audio.volume <= 0f)
            {
                _audio.Stop();
            }
            _budget = Mathf.Min(CellCount, _budget + rain * _dropsPerSecond * Time.deltaTime);
            int count = (int)_budget;
            _budget -= count;
            for (int i = 0; i < count; i++)
            {
                int cell = _cell++ % CellCount;
                if (_covered[cell])
                {
                    continue;
                }
                Vector3 point = CellPosition(position, cell);
                float spacing = _radius * 2f / GridWidth;
                point.x += (NextUnit() - 0.5f) * spacing;
                point.z += (NextUnit() - 0.5f) * spacing;
                point.y += _height;
                float lifetime = Mathf.Clamp((point.y - _surface[cell]) / _fallSpeed, 0f, 1.6f);
                if (lifetime <= 0f)
                {
                    continue;
                }
                var emit = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = new Vector3(snapshot.Wind.x * 0.4f, -_fallSpeed, snapshot.Wind.y * 0.4f),
                    startLifetime = lifetime,
                    startSize = 0.025f,
                    startColor = new Color(0.64f, 0.75f, 0.8f, 0.45f)
                };
                _drops.Emit(emit, 1);
            }

            if (rain > 0f && _splashes != null)
            {
                EmitWaterSplashes(position, rain);
            }
        }

        public void Clear()
        {
            if (_drops != null)
            {
                _drops.Clear();
            }
            if (_splashes != null)
            {
                _splashes.Clear();
            }
            if (_audio != null)
            {
                _audio.Stop();
                _audio.volume = 0f;
            }
            _budget = 0f;
            _hasPosition = false;
        }

        private void Probe(Vector3 position)
        {
            bool hasWater = WorldServices.TryGet<IWorldQuery>(out var query);
            for (int i = 0; i < CellCount; i++)
            {
                Vector3 point = CellPosition(position, i);
                _surface[i] = hasWater ? query.SampleWaterHeight(point) : point.y - _height;
                Vector3 origin = point + Vector3.up * (_height + 30f);
                bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit result,
                    _height + 80f, _coverMask, QueryTriggerInteraction.Ignore);
                _covered[i] = hit && result.point.y > position.y + 0.5f;
                if (hit)
                {
                    _surface[i] = Mathf.Max(_surface[i], result.point.y);
                }
            }
        }

        private void EmitWaterSplashes(Vector3 position, float rain)
        {
            if (!WorldServices.TryGet<IWorldQuery>(out var query) ||
                !WorldServices.TryGet<MapStateNet>(out var map) || map.Config == null)
            {
                return;
            }
            int count = Mathf.Min(4, Mathf.FloorToInt(rain * 60f * Time.deltaTime + NextUnit()));
            float outer = 0f;
            var zones = map.Config.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                outer = Mathf.Max(outer, zones[i].OuterRadius);
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 point = position + new Vector3((NextUnit() - 0.5f) * _radius * 2f, 0f,
                    (NextUnit() - 0.5f) * _radius * 2f);
                if (point.x * point.x + point.z * point.z > outer * outer)
                {
                    continue;
                }
                point.y = query.SampleWaterHeight(point);
                if (Physics.Raycast(point + Vector3.up * 35f, Vector3.down, out RaycastHit hit,
                    35.1f, _coverMask, QueryTriggerInteraction.Ignore) && hit.point.y >= point.y - 0.1f)
                {
                    continue;
                }
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + Vector3.up * 0.03f,
                    startSize = 0.16f,
                    startLifetime = 0.25f,
                    startColor = new Color(0.68f, 0.8f, 0.84f, rain * 0.25f)
                };
                _splashes.Emit(emit, 1);
            }
        }

        private Vector3 CellPosition(Vector3 center, int index)
        {
            float spacing = _radius * 2f / GridWidth;
            return center + new Vector3((index % GridWidth - 2) * spacing, 0f,
                (index / GridWidth - 2) * spacing);
        }

        private ParticleSystem CreateParticles(string objectName, Material material, int cap, bool isSplash)
        {
            if (material == null)
            {
                return null;
            }
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.maxParticles = cap;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = isSplash ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = isSplash ? 0f : 0.035f;
            renderer.lengthScale = 1f;
            particles.Play();
            return particles;
        }

        private AudioClip CreateRainClip()
        {
            const int Samples = 66150;
            var samples = new float[Samples];
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float noise = NextUnit() * 2f - 1f;
                low = Mathf.Lerp(low, noise, 0.12f);
                samples[i] = (noise * 0.15f + low * 0.45f) * 0.65f;
            }
            var clip = AudioClip.Create("WeatherRainNoise", Samples, 1, 22050, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private float NextUnit()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random >> 8) * (1f / 16777216f);
        }
    }
}
