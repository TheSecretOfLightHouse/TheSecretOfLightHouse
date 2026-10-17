using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class WaveModifierModel
    {
        public const int MaxModifiers = 8;

        private const float TwoPi = Mathf.PI * 2f;
        private const float FalloffGradientFactor = 4f;

        public static float HeightAt(ReadOnlySpan<WaveModifierData> modifiers,
            float x, float z, float time = 0f)
        {
            float height = 0f;

            for (int i = 0; i < modifiers.Length; i++)
            {
                Evaluate(in modifiers[i], x, z, time,
                    out float value, out _, out _);
                height += value;
            }

            return height;
        }

        public static void SlopeAt(ReadOnlySpan<WaveModifierData> modifiers,
            float x, float z, out float slopeX, out float slopeZ,
            float time = 0f)
        {
            slopeX = 0f;
            slopeZ = 0f;

            for (int i = 0; i < modifiers.Length; i++)
            {
                Evaluate(in modifiers[i], x, z, time,
                    out _, out float valueX, out float valueZ);
                slopeX += valueX;
                slopeZ += valueZ;
            }
        }

        public static bool AreValid(ReadOnlySpan<WaveModifierData> modifiers)
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

                if (!IsFinite(modifier.Center.x) || !IsFinite(modifier.Center.y)
                    || !IsFinite(modifier.Radius) || modifier.Radius <= 0f
                    || !IsFinite(modifier.Strength) || modifier.Strength < 0f
                    || !IsFinite(modifier.StartTime))
                {
                    return false;
                }

                switch (modifier.Kind)
                {
                    case WaveModifierKind.Bump:
                    case WaveModifierKind.Vortex:
                        break;
                    case WaveModifierKind.RingWave:
                        if (!IsFinite(modifier.BandWidth) || modifier.BandWidth <= 0f
                            || modifier.BandWidth * 0.5f >= modifier.Radius
                            || !IsFinite(modifier.Wavelength) || modifier.Wavelength <= 0f
                            || !IsFinite(modifier.Speed) || modifier.Speed < 0f)
                        {
                            return false;
                        }
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        private static void Evaluate(in WaveModifierData modifier,
            float x, float z, float time,
            out float height, out float slopeX, out float slopeZ)
        {
            height = 0f;
            slopeX = 0f;
            slopeZ = 0f;

            if (modifier.Kind == WaveModifierKind.None || modifier.Radius <= 0f)
            {
                return;
            }

            float offsetX = x - modifier.Center.x;
            float offsetZ = z - modifier.Center.y;
            float distanceSquared = offsetX * offsetX + offsetZ * offsetZ;

            if (modifier.Kind == WaveModifierKind.RingWave)
            {
                EvaluateRing(in modifier, offsetX, offsetZ,
                    distanceSquared, time, out height, out slopeX, out slopeZ);
                return;
            }

            float sign;
            if (modifier.Kind == WaveModifierKind.Bump)
            {
                sign = 1f;
            }
            else if (modifier.Kind == WaveModifierKind.Vortex)
            {
                sign = -1f;
            }
            else
            {
                return;
            }

            float radiusSquared = modifier.Radius * modifier.Radius;
            if (distanceSquared >= radiusSquared)
            {
                return;
            }

            float inside = 1f - distanceSquared / radiusSquared;
            float strength = sign * modifier.Strength;
            height = strength * inside * inside;

            float scale = -FalloffGradientFactor * strength * inside / radiusSquared;
            slopeX = scale * offsetX;
            slopeZ = scale * offsetZ;
        }

        private static void EvaluateRing(in WaveModifierData modifier,
            float offsetX, float offsetZ, float distanceSquared, float time,
            out float height, out float slopeX, out float slopeZ)
        {
            height = 0f;
            slopeX = 0f;
            slopeZ = 0f;

            if (modifier.BandWidth <= 0f || modifier.Wavelength <= 0f)
            {
                return;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            if (distance <= 0f)
            {
                return;
            }

            float halfWidth = modifier.BandWidth * 0.5f;
            float offset = distance - modifier.Radius;
            float u = offset / halfWidth;

            if (Mathf.Abs(u) >= 1f)
            {
                return;
            }

            float inside = 1f - u * u;
            float envelope = inside * inside;
            float envelopeSlope = -FalloffGradientFactor * u * inside / halfWidth;
            float waveNumber = TwoPi / modifier.Wavelength;
            float phase = waveNumber
                * (offset - modifier.Speed * (time - modifier.StartTime));
            float sine = Mathf.Sin(phase);
            float cosine = Mathf.Cos(phase);

            height = modifier.Strength * envelope * sine;

            float radialSlope = modifier.Strength
                * (envelopeSlope * sine + envelope * waveNumber * cosine);

            slopeX = radialSlope * offsetX / distance;
            slopeZ = radialSlope * offsetZ / distance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
