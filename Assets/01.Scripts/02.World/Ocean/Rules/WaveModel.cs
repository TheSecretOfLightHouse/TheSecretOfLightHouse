using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class WaveModel
    {
        public const int MaxWaves = 8;
        public const int MaxModifiers = 8;
        public const float MaxTotalSteepness = 0.8f;
        public const int InverseIterations = 3;

        private const float TwoPi = Mathf.PI * 2f;
        private const float DirectionTolerance = 0.01f;

        public static float HeightAt(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float x,
            float z,
            float time)
        {
            FindRestPosition(waves, x, z, time, out float restX, out float restZ);
            Vector3 displacement = DisplacementAt(waves, restX, restZ, time);
            return displacement.y;
        }

        public static Vector3 NormalAt(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float x,
            float z,
            float time)
        {
            FindRestPosition(waves, x, z, time, out float restX, out float restZ);

            float normalX = 0f;
            float normalY = 1f;
            float normalZ = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                ref readonly WaveParams wave = ref waves[i];
                if (wave.Wavelength <= 0f)
                {
                    continue;
                }

                float waveNumber = TwoPi / wave.Wavelength;
                float phase = waveNumber * (wave.Direction.x * restX + wave.Direction.y * restZ - wave.Speed * time);
                float slope = waveNumber * wave.Amplitude * Mathf.Cos(phase);

                normalX -= wave.Direction.x * slope;
                normalZ -= wave.Direction.y * slope;
                normalY -= wave.Steepness * Mathf.Sin(phase);
            }

            return new Vector3(normalX, normalY, normalZ).normalized;
        }

        public static Vector3 DisplacementAt(ReadOnlySpan<WaveParams> waves, float restX, float restZ, float time)
        {
            float displacementX = 0f;
            float displacementY = 0f;
            float displacementZ = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                ref readonly WaveParams wave = ref waves[i];
                if (wave.Wavelength <= 0f)
                {
                    continue;
                }

                float waveNumber = TwoPi / wave.Wavelength;
                float phase = waveNumber * (wave.Direction.x * restX + wave.Direction.y * restZ - wave.Speed * time);
                float horizontal = wave.Steepness / waveNumber * Mathf.Cos(phase);

                displacementX += wave.Direction.x * horizontal;
                displacementZ += wave.Direction.y * horizontal;
                displacementY += wave.Amplitude * Mathf.Sin(phase);
            }

            return new Vector3(displacementX, displacementY, displacementZ);
        }

        public static bool IsValid(ReadOnlySpan<WaveParams> waves)
        {
            if (waves.Length > MaxWaves)
            {
                return false;
            }

            float totalSteepness = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                ref readonly WaveParams wave = ref waves[i];

                if (wave.Wavelength <= 0f || wave.Amplitude < 0f)
                {
                    return false;
                }

                if (wave.Steepness < 0f || wave.Steepness > 1f)
                {
                    return false;
                }

                if (Mathf.Abs(wave.Direction.sqrMagnitude - 1f) > DirectionTolerance)
                {
                    return false;
                }

                totalSteepness += wave.Steepness;
            }

            return totalSteepness <= MaxTotalSteepness;
        }

        private static void FindRestPosition(
            ReadOnlySpan<WaveParams> waves,
            float x,
            float z,
            float time,
            out float restX,
            out float restZ)
        {
            restX = x;
            restZ = z;

            for (int iteration = 0; iteration < InverseIterations; iteration++)
            {
                Vector3 displacement = DisplacementAt(waves, restX, restZ, time);
                restX = x - displacement.x;
                restZ = z - displacement.z;
            }
        }
    }
}
