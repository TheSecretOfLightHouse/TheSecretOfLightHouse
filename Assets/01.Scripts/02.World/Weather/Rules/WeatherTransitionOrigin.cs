using System;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public struct WeatherTransitionOrigin : IEquatable<WeatherTransitionOrigin>
    {
        public Vector4 AmplitudeLow;
        public Vector4 AmplitudeHigh;
        public Vector4 SteepnessLow;
        public Vector4 SteepnessHigh;
        [System.NonSerialized] public Vector4 ResponseLow;
        [System.NonSerialized] public Vector4 ResponseHigh;
        public Vector2 Wind;
        public float Fog;
        public float Rain;
        public float Lightning;
        public float TornadoChance;
        public float StormIntensity;

        public bool Equals(WeatherTransitionOrigin other)
        {
            return AmplitudeLow.Equals(other.AmplitudeLow)
                && AmplitudeHigh.Equals(other.AmplitudeHigh)
                && SteepnessLow.Equals(other.SteepnessLow)
                && SteepnessHigh.Equals(other.SteepnessHigh)
                && Wind.Equals(other.Wind)
                && Fog.Equals(other.Fog)
                && Rain.Equals(other.Rain)
                && Lightning.Equals(other.Lightning)
                && TornadoChance.Equals(other.TornadoChance)
                && StormIntensity.Equals(other.StormIntensity);
        }

        public override bool Equals(object obj)
        {
            return obj is WeatherTransitionOrigin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(AmplitudeLow, AmplitudeHigh, SteepnessLow, SteepnessHigh),
                Wind, Fog, Rain, Lightning, TornadoChance, StormIntensity);
        }

        public static WeatherTransitionOrigin Capture(in WeatherSnapshot snapshot)
        {
            var value = new WeatherTransitionOrigin
            {
                Wind = snapshot.Wind,
                Fog = snapshot.Fog,
                Rain = snapshot.Rain,
                Lightning = snapshot.Lightning,
                TornadoChance = snapshot.TornadoChance,
                StormIntensity = snapshot.StormIntensity
            };

            for (int i = 0; i < snapshot.Waves.Length; i++)
            {
                if (i < 4)
                {
                    value.AmplitudeLow[i] = snapshot.Waves[i].Amplitude;
                    value.SteepnessLow[i] = snapshot.Waves[i].Steepness;
                    value.ResponseLow[i] = snapshot.Waves[i].Response;
                }
                else
                {
                    value.AmplitudeHigh[i - 4] = snapshot.Waves[i].Amplitude;
                    value.SteepnessHigh[i - 4] = snapshot.Waves[i].Steepness;
                    value.ResponseHigh[i - 4] = snapshot.Waves[i].Response;
                }
            }

            return value;
        }

        public void Apply(ref WeatherSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Waves.Length; i++)
            {
                snapshot.Waves[i].Amplitude = i < 4 ? AmplitudeLow[i] : AmplitudeHigh[i - 4];
                snapshot.Waves[i].Steepness = i < 4 ? SteepnessLow[i] : SteepnessHigh[i - 4];
            }

            snapshot.Wind = Wind;
            snapshot.Fog = Fog;
            snapshot.Rain = Rain;
            snapshot.Lightning = Lightning;
            snapshot.TornadoChance = TornadoChance;
            snapshot.StormIntensity = StormIntensity;
        }

        public static WeatherTransitionOrigin Lerp(
            in WeatherTransitionOrigin from, in WeatherTransitionOrigin to, float t)
        {
            float blend = Mathf.Clamp01(t);
            Vector4 amplitudeLow = default;
            Vector4 amplitudeHigh = default;
            Vector4 steepnessLow = default;
            Vector4 steepnessHigh = default;
            for (int i = 0; i < 4; i++)
            {
                float low = WaveResponse.Shape(blend, to.ResponseLow[i]);
                float high = WaveResponse.Shape(blend, to.ResponseHigh[i]);
                amplitudeLow[i] = Mathf.Lerp(from.AmplitudeLow[i], to.AmplitudeLow[i], low);
                steepnessLow[i] = Mathf.Lerp(from.SteepnessLow[i], to.SteepnessLow[i], low);
                amplitudeHigh[i] = Mathf.Lerp(from.AmplitudeHigh[i], to.AmplitudeHigh[i], high);
                steepnessHigh[i] = Mathf.Lerp(from.SteepnessHigh[i], to.SteepnessHigh[i], high);
            }

            return new WeatherTransitionOrigin
            {
                AmplitudeLow = amplitudeLow,
                AmplitudeHigh = amplitudeHigh,
                SteepnessLow = steepnessLow,
                SteepnessHigh = steepnessHigh,
                ResponseLow = to.ResponseLow,
                ResponseHigh = to.ResponseHigh,
                Wind = Vector2.Lerp(from.Wind, to.Wind, blend),
                Fog = Mathf.Lerp(from.Fog, to.Fog, blend),
                Rain = Mathf.Lerp(from.Rain, to.Rain, blend),
                Lightning = Mathf.Lerp(from.Lightning, to.Lightning, blend),
                TornadoChance = Mathf.Lerp(from.TornadoChance, to.TornadoChance, blend),
                StormIntensity = Mathf.Lerp(from.StormIntensity, to.StormIntensity, blend)
            };
        }
    }
}
