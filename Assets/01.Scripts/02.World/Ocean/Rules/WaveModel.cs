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
        private const float FalloffGradientFactor = 4f;

        public static float HeightAt(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float x,
            float z,
            float time)
        {
            FindRestPosition(waves, x, z, time, out float restX, out float restZ);
            Vector3 displacement = DisplacementAt(waves, restX, restZ, time);
            return displacement.y + ModifierHeightAt(modifiers, x, z);
        }

        public static Vector3 NormalAt(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float x,
            float z,
            float time)
        {
            FindRestPosition(waves, x, z, time, out float restX, out float restZ);

            float slopeX = 0f;
            float slopeZ = 0f;
            float stretchXX = 0f;
            float stretchZZ = 0f;
            float stretchXZ = 0f;

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
                float stretch = wave.Steepness * Mathf.Sin(phase);

                slopeX += wave.Direction.x * slope;
                slopeZ += wave.Direction.y * slope;
                stretchXX += wave.Direction.x * wave.Direction.x * stretch;
                stretchZZ += wave.Direction.y * wave.Direction.y * stretch;
                stretchXZ += wave.Direction.x * wave.Direction.y * stretch;
            }

            float normalY = (1f - stretchXX) * (1f - stretchZZ) - stretchXZ * stretchXZ;
            float normalX = -(slopeX * (1f - stretchZZ) + stretchXZ * slopeZ);
            float normalZ = -(slopeZ * (1f - stretchXX) + stretchXZ * slopeX);

            ModifierSlopeAt(modifiers, x, z, out float modifierSlopeX, out float modifierSlopeZ);
            normalX -= modifierSlopeX * normalY;
            normalZ -= modifierSlopeZ * normalY;

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

        public static float ModifierHeightAt(ReadOnlySpan<WaveModifierData> modifiers, float x, float z)
        {
            float height = 0f;

            for (int i = 0; i < modifiers.Length; i++)
            {
                ref readonly WaveModifierData modifier = ref modifiers[i];
                int sign = GetSign(modifier.Kind);
                if (sign == 0 || modifier.Radius <= 0f)
                {
                    continue;
                }

                float offsetX = x - modifier.Center.x;
                float offsetZ = z - modifier.Center.y;
                float radiusSqr = modifier.Radius * modifier.Radius;
                float distanceSqr = offsetX * offsetX + offsetZ * offsetZ;
                if (distanceSqr >= radiusSqr)
                {
                    continue;
                }

                float inside = 1f - distanceSqr / radiusSqr;
                height += sign * modifier.Strength * inside * inside;
            }

            return height;
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

        public static bool AreModifiersValid(ReadOnlySpan<WaveModifierData> modifiers)
        {
            if (modifiers.Length > MaxModifiers)
            {
                return false;
            }

            for (int i = 0; i < modifiers.Length; i++)
            {
                ref readonly WaveModifierData modifier = ref modifiers[i];
                if (modifier.Kind == WaveModifierKind.None)
                {
                    continue;
                }

                if (modifier.Radius <= 0f || modifier.Strength < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ModifierSlopeAt(
            ReadOnlySpan<WaveModifierData> modifiers,
            float x,
            float z,
            out float slopeX,
            out float slopeZ)
        {
            slopeX = 0f;
            slopeZ = 0f;

            for (int i = 0; i < modifiers.Length; i++)
            {
                ref readonly WaveModifierData modifier = ref modifiers[i];
                int sign = GetSign(modifier.Kind);
                if (sign == 0 || modifier.Radius <= 0f)
                {
                    continue;
                }

                float offsetX = x - modifier.Center.x;
                float offsetZ = z - modifier.Center.y;
                float radiusSqr = modifier.Radius * modifier.Radius;
                float distanceSqr = offsetX * offsetX + offsetZ * offsetZ;
                if (distanceSqr >= radiusSqr)
                {
                    continue;
                }

                float inside = 1f - distanceSqr / radiusSqr;
                float scale = -FalloffGradientFactor * sign * modifier.Strength * inside / radiusSqr;
                slopeX += scale * offsetX;
                slopeZ += scale * offsetZ;
            }
        }

        private static int GetSign(WaveModifierKind kind)
        {
            switch (kind)
            {
                case WaveModifierKind.Bump:
                    return 1;
                case WaveModifierKind.Vortex:
                    return -1;
                default:
                    return 0;
            }
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
