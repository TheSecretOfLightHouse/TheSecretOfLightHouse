using Lighthouse.World.Clock.View;
using Lighthouse.World.Weather.Net;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Weather.View
{
    [DisallowMultipleComponent]
    public sealed class WeatherLightningView : MonoBehaviour
    {
        private const int Capacity = 4;
        private const int LinesPerBolt = 2 + LightningBoltShape.BranchCount;
        private const int HaloIndex = LinesPerBolt - 1;
        private const float HaloWidthRatio = 9f;
        private const float HaloAlpha = 0.5f;
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private const float RestrikeDelay = 0.08f;
        private const float RestrikeStrength = 0.85f;
        private const float MainDecay = 16f;
        private const float RestrikeDecay = 14f;
        private const float SkyDirectionHeightRatio = 0.6f;
        private const float BranchWidthRatio = 0.4f;

        [SerializeField] private WeatherLightningNet _events;
        [SerializeField] private DayNightSunView _sun;
        [SerializeField] private Material _material;
        [SerializeField] private Material _boltMaterial;
        [SerializeField] private AudioClip _thunderClip;
        [SerializeField, Min(0.01f)] private float _flashSeconds = 0.28f;
        [SerializeField, Min(0f)] private float _flashIntensity = 0.65f;
        [SerializeField, Min(1f)] private float _thunderRange = 500f;
        [SerializeField, Min(0f)] private float _thunderVolume = 0.5f;
        [SerializeField, Min(10f)] private float _boltHeight = 220f;
        [SerializeField, Min(0.1f)] private float _boltWidth = 3f;
        [SerializeField, Min(1f)] private float _boltDistanceScale = 2.2f;

        private readonly LineRenderer[] _lines = new LineRenderer[Capacity * LinesPerBolt];
        private readonly AudioSource[] _sounds = new AudioSource[Capacity];
        private readonly WeatherLightningEvent[] _active = new WeatherLightningEvent[Capacity];
        private readonly Vector3[] _grounds = new Vector3[Capacity];
        private readonly Color[] _colors = new Color[Capacity];
        private readonly Color[] _haloColors = new Color[Capacity];
        private readonly double[] _thunderAt = new double[Capacity];
        private readonly Vector3[] _mainPoints = new Vector3[LightningBoltShape.MainPointCount];
        private readonly Vector3[] _branchPoints = new Vector3[LightningBoltShape.BranchPointCount];
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
            Material boltMaterial = _boltMaterial != null ? _boltMaterial : _material;
            var taper = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.6f));
            var softEdge = new MaterialPropertyBlock();
            softEdge.SetFloat(SoftId, 1f);
            for (int i = 0; i < _lines.Length; i++)
            {
                var child = new GameObject("WeatherLightning");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = boltMaterial;
                line.useWorldSpace = true;
                line.widthCurve = taper;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                int part = i % LinesPerBolt;
                line.positionCount = part == 0 || part == HaloIndex
                    ? LightningBoltShape.MainPointCount : LightningBoltShape.BranchPointCount;
                if (part == HaloIndex)
                {
                    line.SetPropertyBlock(softEdge);
                }
                line.enabled = false;
                _lines[i] = line;
            }
            for (int i = 0; i < Capacity; i++)
            {
                var sound = _lines[i * LinesPerBolt].gameObject.AddComponent<AudioSource>();
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
            float skyAmount = 0f;
            Vector3 skyDirection = Vector3.zero;
            double now = NetworkTime.time;
            for (int i = 0; i < Capacity; i++)
            {
                double age = now - _active[i].Time;
                bool visible = _active[i].Id != 0 && age >= 0d && age < _flashSeconds;
                if (visible)
                {
                    float pulse = Pulse((float)age);
                    ApplyBoltColor(i, pulse);
                    flash = Mathf.Max(flash, pulse * _flashIntensity);
                    if (pulse > skyAmount && _observer != null)
                    {
                        skyAmount = pulse;
                        Vector3 mid = _grounds[i] + Vector3.up * (_boltHeight * SkyDirectionHeightRatio);
                        skyDirection = (mid - _observer.transform.position).normalized;
                    }
                }
                SetBoltVisible(i, visible);
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
            LightningSkyFlash.Set(skyAmount, skyDirection);
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
            for (int i = 0; i < _lines.Length; i++)
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
                SetBoltVisible(i, false);
                if (_sounds[i] != null)
                {
                    _sounds[i].Stop();
                }
            }
            LightningSkyFlash.Clear();
            if (_sun != null)
            {
                _sun.SetWeatherFlash(0f);
            }
        }

        private static float Pulse(float age)
        {
            float main = Mathf.Exp(-age * MainDecay);
            float restrike = age > RestrikeDelay
                ? RestrikeStrength * Mathf.Exp(-(age - RestrikeDelay) * RestrikeDecay) : 0f;
            return Mathf.Max(main, restrike);
        }

        private void SetBoltVisible(int slot, bool isVisible)
        {
            for (int j = 0; j < LinesPerBolt; j++)
            {
                LineRenderer line = _lines[slot * LinesPerBolt + j];
                if (line != null)
                {
                    line.enabled = isVisible;
                }
            }
        }

        private void ApplyBoltColor(int slot, float alpha)
        {
            Color core = _colors[slot];
            core.a = alpha;
            Color halo = _haloColors[slot];
            halo.a = alpha * HaloAlpha;
            for (int j = 0; j < LinesPerBolt; j++)
            {
                LineRenderer line = _lines[slot * LinesPerBolt + j];
                Color color = j == HaloIndex ? halo : core;
                line.startColor = color;
                line.endColor = color;
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
            _colors[slot] = lightning.IsAbyss ? new Color(0.75f, 1f, 0.85f) : Color.white;
            _haloColors[slot] = lightning.IsAbyss ? new Color(0.2f, 1f, 0.43f) : new Color(0.55f, 0.35f, 1f);
            Vector3 ground = lightning.Position;
            ground.x *= _boltDistanceScale;
            ground.z *= _boltDistanceScale;
            _grounds[slot] = ground;
            ShapeBolt(slot, ground, lightning);
            double thunderAt = lightning.Time + Vector3.Distance(_observer.transform.position, lightning.Position) / 343d;
            _thunderAt[slot] = thunderAt >= now ? thunderAt : double.PositiveInfinity;
        }

        private void ShapeBolt(int slot, Vector3 ground, WeatherLightningEvent lightning)
        {
            float jitter = _boltHeight * 0.06f;
            LightningBoltShape.FillMain(_mainPoints, ground, _boltHeight, jitter, lightning.Seed, lightning.Id);
            LineRenderer main = _lines[slot * LinesPerBolt];
            main.SetPositions(_mainPoints);
            main.widthMultiplier = _boltWidth;
            LineRenderer halo = _lines[slot * LinesPerBolt + HaloIndex];
            halo.SetPositions(_mainPoints);
            halo.widthMultiplier = _boltWidth * HaloWidthRatio;
            for (int b = 0; b < LightningBoltShape.BranchCount; b++)
            {
                LightningBoltShape.FillBranch(_branchPoints, _mainPoints, b, _boltHeight, jitter,
                    lightning.Seed, lightning.Id);
                LineRenderer branch = _lines[slot * LinesPerBolt + 1 + b];
                branch.SetPositions(_branchPoints);
                branch.widthMultiplier = _boltWidth * BranchWidthRatio;
            }
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
