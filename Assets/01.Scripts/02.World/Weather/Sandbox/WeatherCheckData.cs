using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherCheckData
    {
        public const int SlotCount = 3;

        private const float Tolerance = 1e-4f;

        private static readonly Vector2[] Directions =
        {
            Vector2.right,
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0.6f, 0.8f)
        };

        public static WeatherSnapshot MakeCalm()
        {
            return Make(0.5f, 0.1f, 2f, 0.1f, 0f);
        }

        public static WeatherSnapshot MakeOvercast()
        {
            return Make(1f, 0.15f, 4f, 0.3f, 0.15f);
        }

        public static WeatherSnapshot MakeRain()
        {
            return Make(1.5f, 0.2f, 8f, 0.5f, 0.4f);
        }

        public static WeatherSnapshot MakeStorm()
        {
            return Make(2.5f, 0.25f, 15f, 0.9f, 0.8f);
        }

        public static WeatherSnapshot MakeMaelstrom()
        {
            return Make(4f, 0.26f, 20f, 1f, 1f);
        }

        public static WeatherSnapshot Make(float amplitude, float steepness, float windX, float scalar, float intensity)
        {
            WeatherSnapshot snapshot = WeatherSnapshot.CreateBuffer(SlotCount);

            for (int i = 0; i < SlotCount; i++)
            {
                snapshot.Waves[i] = new WaveParams
                {
                    Amplitude = amplitude * (i + 1),
                    Wavelength = 40f / (i + 1),
                    Direction = Directions[i],
                    Steepness = steepness,
                    Speed = 3f + i
                };
            }

            snapshot.Wind = new Vector2(windX, 0f);
            snapshot.Fog = scalar;
            snapshot.Rain = scalar * 0.5f;
            snapshot.Lightning = scalar * 10f;
            snapshot.TornadoChance = scalar * 0.2f;
            snapshot.StormIntensity = intensity;
            return snapshot;
        }

        public static bool Matches(in WeatherSnapshot x, in WeatherSnapshot y)
        {
            if (x.Waves.Length != y.Waves.Length)
            {
                return false;
            }

            bool isMatch = Near(x.Wind.x, y.Wind.x) && Near(x.Wind.y, y.Wind.y)
                && Near(x.Fog, y.Fog) && Near(x.Rain, y.Rain) && Near(x.Lightning, y.Lightning)
                && Near(x.TornadoChance, y.TornadoChance) && Near(x.StormIntensity, y.StormIntensity);

            for (int i = 0; i < x.Waves.Length; i++)
            {
                WaveParams p = x.Waves[i];
                WaveParams q = y.Waves[i];

                isMatch &= Near(p.Amplitude, q.Amplitude) && Near(p.Steepness, q.Steepness)
                    && Near(p.Wavelength, q.Wavelength) && Near(p.Speed, q.Speed)
                    && Near(p.Direction.x, q.Direction.x) && Near(p.Direction.y, q.Direction.y);
            }

            return isMatch;
        }

        public static bool Near(float x, float y)
        {
            return Mathf.Abs(x - y) <= Tolerance;
        }
    }
}
