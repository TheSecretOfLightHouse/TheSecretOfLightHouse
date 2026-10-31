using Lighthouse.World.Clock.View;
using Lighthouse.World.MapGeneration.View;
using Lighthouse.World.Weather.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    [DisallowMultipleComponent]
    public sealed class WeatherLightningView : MonoBehaviour
    {
        private const int Capacity = 4;
        private const int PointCount = 9;
        [SerializeField] private WeatherLightningNet _events;
        [SerializeField] private DayNightSunView _sun;
        [SerializeField] private Material _material;
        [SerializeField] private AudioClip _thunderClip;
        [SerializeField, Min(0.01f)] private float _flashSeconds = 0.28f;
        [SerializeField, Min(0f)] private float _flashIntensity = 0.65f;
        [SerializeField, Min(1f)] private float _thunderRange = 500f;
        [SerializeField, Min(0f)] private float _thunderVolume = 0.5f;

        private readonly LineRenderer[] _lines = new LineRenderer[Capacity];
        private readonly AudioSource[] _sounds = new AudioSource[Capacity];
        private readonly WeatherLightningEvent[] _active = new WeatherLightningEvent[Capacity];
        private readonly double[] _thunderAt = new double[Capacity];
        private readonly Vector3[] _points = new Vector3[PointCount];
        private Camera _observer;
        private uint _lastId;
        private int _nextSlot;
        private AudioClip _generatedClip;
        private WorldAudioProfile _audioProfile;

        public uint LastEventId => _lastId;

        private void OnEnable()
        {
            _lastId = 0;
            if (_events != null)
            {
                _events.LightningOccurred += HandleLightning;
                _events.Stopped += Clear;
            }
            _audioProfile = Resources.Load<WorldAudioProfile>(WorldAudioProfile.ResourceName);
            if (_thunderClip == null && _audioProfile == null)
            {
                _generatedClip = CreateThunderClip();
            }
            for (int i = 0; i < Capacity; i++)
            {
                var child = new GameObject("WeatherLightning");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = true;
                line.widthMultiplier = 0.22f;
                line.positionCount = PointCount;
                line.enabled = false;
                _lines[i] = line;
                var sound = child.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 0f;
                sound.clip = _thunderClip != null ? _thunderClip : _generatedClip;
                if (_audioProfile != null)
                {
                    sound.outputAudioMixerGroup = _audioProfile.EffectsMixer;
                }
                _sounds[i] = sound;
                _thunderAt[i] = double.PositiveInfinity;
            }
        }

        private void LateUpdate()
        {
            float flash = 0f;
            double now = NetworkTime.time;
            for (int i = 0; i < Capacity; i++)
            {
                double age = now - _active[i].Time;
                bool visible = _active[i].Id != 0 && age >= 0d && age < _flashSeconds;
                _lines[i].enabled = visible;
                if (visible)
                {
                    float envelope = Mathf.Clamp01(1f - (float)age / _flashSeconds);
                    flash = Mathf.Max(flash, envelope * _flashIntensity);
                }
                if (now >= _thunderAt[i])
                {
                    if (now - _thunderAt[i] <= 0.2d && _observer != null)
                    {
                        float distance = Vector3.Distance(_observer.transform.position, _active[i].Position);
                        _sounds[i].volume = Mathf.Clamp01(1f - distance / _thunderRange) * _thunderVolume;
                        if (_sounds[i].clip != null)
                        {
                            _sounds[i].Play();
                        }
                    }
                    _thunderAt[i] = double.PositiveInfinity;
                }
            }
            if (_sun != null)
            {
                _sun.SetWeatherFlash(flash);
            }
        }

        private void OnDisable()
        {
            if (_events != null)
            {
                _events.LightningOccurred -= HandleLightning;
                _events.Stopped -= Clear;
            }
            Clear();
            for (int i = 0; i < Capacity; i++)
            {
                if (_lines[i] != null)
                {
                    Destroy(_lines[i].gameObject);
                }
            }
            if (_generatedClip != null)
            {
                Destroy(_generatedClip);
            }
        }

        public void SetObserver(Camera observer)
        {
            _observer = observer;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _active[i] = default;
                _thunderAt[i] = double.PositiveInfinity;
                if (_lines[i] != null)
                {
                    _lines[i].enabled = false;
                }
                if (_sounds[i] != null)
                {
                    _sounds[i].Stop();
                }
            }
            if (_sun != null)
            {
                _sun.SetWeatherFlash(0f);
            }
        }

        private void HandleLightning(WeatherLightningEvent lightning)
        {
            if (lightning.Id <= _lastId)
            {
                return;
            }
            _lastId = lightning.Id;
            double now = NetworkTime.time;
            if (_observer == null || now - lightning.Time > 8d || lightning.Time > now + 1d)
            {
                return;
            }
            int slot = _nextSlot++ % Capacity;
            if (_audioProfile != null)
            {
                _sounds[slot].clip = _audioProfile.PickThunder(lightning.IsAbyss);
            }
            _active[slot] = lightning;
            StormLightningPattern.Fill(_points, 0f, 20f, 45f, 6f, lightning.Seed, lightning.Id, 0);
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i] += lightning.Position;
            }
            _lines[slot].SetPositions(_points);
            Color color = lightning.IsAbyss ? new Color(0.2f, 1f, 0.43f) : new Color(0.72f, 0.83f, 1f);
            _lines[slot].startColor = color;
            _lines[slot].endColor = color;
            double thunderAt = lightning.Time + Vector3.Distance(_observer.transform.position, lightning.Position) / 343d;
            _thunderAt[slot] = thunderAt >= now ? thunderAt : double.PositiveInfinity;
        }

        private AudioClip CreateThunderClip()
        {
            const int Samples = 66150;
            var samples = new float[Samples];
            uint random = 0x89ABu;
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                random ^= random << 13;
                random ^= random >> 17;
                random ^= random << 5;
                float noise = (random >> 8) * (2f / 16777216f) - 1f;
                low = Mathf.Lerp(low, noise, 0.015f);
                float t = i / 22050f;
                float envelope = Mathf.Min(1f, t * 30f) * Mathf.Exp(-t * 1.4f);
                samples[i] = (low * 2.5f + noise * 0.07f) * envelope;
            }
            var clip = AudioClip.Create("WeatherThunderNoise", Samples, 1, 22050, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
