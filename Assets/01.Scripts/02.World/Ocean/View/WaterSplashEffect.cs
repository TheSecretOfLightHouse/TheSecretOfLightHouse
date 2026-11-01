using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    public sealed class WaterSplashEffect : MonoBehaviour
    {
        [SerializeField] private WaterSplashKind _kind = WaterSplashKind.ImpactColumn;
        [SerializeField] private ParticleSystem[] _systems;
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.6f, 1.5f);

        private int[] _baseCounts;
        private float[] _burstTimes;
        private Color[] _baseColors;
        private float _duration;

        public WaterSplashKind Kind => _kind;
        public bool IsActive { get; private set; }
        public float StartTime { get; private set; }
        public float Score { get; private set; }
        public int Cost { get; private set; }
        public float Duration => _duration;
        public float LifeCap { get; private set; }

        private void Awake()
        {
            if (_systems == null || _systems.Length == 0)
            {
                Debug.LogError($"[{nameof(WaterSplashEffect)}] Particle systems are required.");
                enabled = false;
                return;
            }
            _baseCounts = new int[_systems.Length];
            _burstTimes = new float[_systems.Length];
            _baseColors = new Color[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null)
                {
                    Debug.LogError($"[{nameof(WaterSplashEffect)}] Missing particle system at {i}.");
                    enabled = false;
                    return;
                }
                ParticleSystem.EmissionModule emission = _systems[i].emission;
                ParticleSystem.Burst burst = emission.GetBurst(0);
                _baseCounts[i] = Mathf.Max(1, Mathf.RoundToInt(burst.count.constantMax));
                _burstTimes[i] = burst.time;
                _baseColors[i] = _systems[i].main.startColor.color;
                _duration = Mathf.Max(_duration, burst.time + _systems[i].main.startLifetime.constantMax);
            }
        }

        public int EstimateCost(float qualityScale)
        {
            if (_baseCounts == null)
            {
                return 0;
            }
            int total = 0;
            for (int i = 0; i < _baseCounts.Length; i++)
            {
                total += ScaledCount(_baseCounts[i], qualityScale);
            }
            return total;
        }

        public void Begin(Vector3 position, Vector3 normal, float strength, float qualityScale, float score, int cost, float time, float sizeScale, float alphaScale, float lifeCap)
        {
            transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, normal));
            transform.localScale = Vector3.one * Mathf.Lerp(_scaleRange.x, _scaleRange.y, strength) * sizeScale;
            gameObject.SetActive(true);
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                Color color = _baseColors[i];
                color.a *= alphaScale;
                main.startColor = color;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.SetBurst(0, new ParticleSystem.Burst(_burstTimes[i], (short)ScaledCount(_baseCounts[i], qualityScale)));
                system.Play(false);
            }
            IsActive = true;
            StartTime = time;
            Score = score;
            Cost = cost;
            LifeCap = lifeCap;
        }

        public void End()
        {
            for (int i = 0; i < _systems.Length; i++)
            {
                _systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            gameObject.SetActive(false);
            IsActive = false;
            Cost = 0;
        }

        private static int ScaledCount(int baseCount, float qualityScale)
        {
            if (baseCount <= 1)
            {
                return baseCount;
            }
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * qualityScale));
        }
    }
}
