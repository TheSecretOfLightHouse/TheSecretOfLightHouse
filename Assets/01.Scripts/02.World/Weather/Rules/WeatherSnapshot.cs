using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public struct WeatherSnapshot
    {
        public WaveParams[] Waves;
        public Vector2 Wind;
        public float Fog;
        public float Rain;
        public float Lightning;
        public float TornadoChance;
        public float StormIntensity;

        public ReadOnlySpan<WaveParams> WaveSpan =>
            Waves != null ? new ReadOnlySpan<WaveParams>(Waves) : ReadOnlySpan<WaveParams>.Empty;

        public static WeatherSnapshot CreateBuffer(int waveCount)
        {
            return new WeatherSnapshot { Waves = new WaveParams[Mathf.Clamp(waveCount, 0, WaveModel.MaxWaves)] };
        }

        public static bool TryCreate(WaveSlotLayout layout, WeatherProfile profile, out WeatherSnapshot snapshot)
        {
            if (layout == null || profile == null || layout.Count != profile.LevelCount)
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateBuffer(layout.Count);

            for (int i = 0; i < snapshot.Waves.Length; i++)
            {
                WaveSlot slot = layout.GetSlot(i);
                WaveLevel level = profile.GetLevel(i);

                snapshot.Waves[i] = new WaveParams
                {
                    Amplitude = level.Amplitude,
                    Wavelength = slot.Wavelength,
                    Direction = slot.Direction,
                    Steepness = level.Steepness,
                    Speed = slot.Speed
                };
            }

            snapshot.Wind = profile.Wind;
            snapshot.Fog = profile.FogDensity;
            snapshot.Rain = profile.RainRate;
            snapshot.Lightning = profile.LightningRate;
            snapshot.TornadoChance = profile.TornadoChance;
            snapshot.StormIntensity = profile.StormIntensity;
            return true;
        }

        public static bool TryLerp(in WeatherSnapshot a, in WeatherSnapshot b, float t, ref WeatherSnapshot result)
        {
            WaveParams[] from = a.Waves;
            WaveParams[] to = b.Waves;
            WaveParams[] into = result.Waves;

            if (from == null || to == null || into == null || from.Length != to.Length || into.Length != to.Length)
            {
                return false;
            }

            float k = Mathf.Clamp01(t);

            for (int i = 0; i < to.Length; i++)
            {
                WaveParams wave = to[i];
                wave.Amplitude = Mathf.Lerp(from[i].Amplitude, to[i].Amplitude, k);
                wave.Steepness = Mathf.Lerp(from[i].Steepness, to[i].Steepness, k);
                into[i] = wave;
            }

            result.Wind = Vector2.Lerp(a.Wind, b.Wind, k);
            result.Fog = Mathf.Lerp(a.Fog, b.Fog, k);
            result.Rain = Mathf.Lerp(a.Rain, b.Rain, k);
            result.Lightning = Mathf.Lerp(a.Lightning, b.Lightning, k);
            result.TornadoChance = Mathf.Lerp(a.TornadoChance, b.TornadoChance, k);
            result.StormIntensity = Mathf.Lerp(a.StormIntensity, b.StormIntensity, k);
            return true;
        }
    }
}
