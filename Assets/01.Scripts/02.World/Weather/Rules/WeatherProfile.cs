using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    [CreateAssetMenu(fileName = "WeatherProfile", menuName = "TheLightHouse/World/WeatherProfile")]
    public class WeatherProfile : ScriptableObject
    {
        [SerializeField]
        private WaveLevel[] _levels =
        {
            new WaveLevel { Amplitude = 0.5f, Steepness = 0.2f },
            new WaveLevel { Amplitude = 0.25f, Steepness = 0.15f },
            new WaveLevel { Amplitude = 0.12f, Steepness = 0.1f },
            new WaveLevel { Amplitude = 0.05f, Steepness = 0.05f }
        };

        [SerializeField] private Vector2 _wind = new Vector2(2f, 0f);
        [SerializeField, Range(0f, 1f)] private float _fogDensity = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _rainRate;
        [SerializeField, Min(0f)] private float _lightningRate;
        [SerializeField, Range(0f, 1f)] private float _tornadoChance;
        [SerializeField, Range(0f, 1f)] private float _stormIntensity;

        public int LevelCount => _levels != null ? _levels.Length : 0;
        public Vector2 Wind => _wind;
        public float FogDensity => _fogDensity;
        public float RainRate => _rainRate;
        public float LightningRate => _lightningRate;
        public float TornadoChance => _tornadoChance;
        public float StormIntensity => _stormIntensity;

        public WaveLevel GetLevel(int index)
        {
            return index >= 0 && index < LevelCount ? _levels[index] : default;
        }

        public float GetTotalSteepness()
        {
            float total = 0f;

            for (int i = 0; i < LevelCount; i++)
            {
                total += _levels[i].Steepness;
            }

            return total;
        }

        private void OnValidate()
        {
            if (_levels == null)
            {
                return;
            }

            if (_levels.Length > WaveModel.MaxWaves)
            {
                Array.Resize(ref _levels, WaveModel.MaxWaves);
            }

            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i].Amplitude = Mathf.Max(0f, _levels[i].Amplitude);
                _levels[i].Steepness = Mathf.Clamp01(_levels[i].Steepness);
            }

            if (GetTotalSteepness() > WaveModel.MaxTotalSteepness)
            {
                Debug.LogWarning($"[{nameof(WeatherProfile)}] {name}: steepness sum exceeds {WaveModel.MaxTotalSteepness}.", this);
            }
        }
    }
}
