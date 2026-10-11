using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class WaveModifierModel
    {
        public const int MaxModifiers = 8;

        private const float FalloffGradientFactor = 4f;

        public static float HeightAt(ReadOnlySpan<WaveModifierData> modifiers, float x, float z)
        {
            float height = 0f;

            for (int i = 0; i < modifiers.Length; i++)
            {
                if (!TryGetFalloff(in modifiers[i], x, z, out float signedStrength, out _, out _, out float inside))
                {
                    continue;
                }

                height += signedStrength * inside * inside;
            }

            return height;
        }

        public static void SlopeAt(
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
                if (!TryGetFalloff(in modifier, x, z, out float signedStrength, out float offsetX, out float offsetZ, out float inside))
                {
                    continue;
                }

                float scale = -FalloffGradientFactor * signedStrength * inside / (modifier.Radius * modifier.Radius);
                slopeX += scale * offsetX;
                slopeZ += scale * offsetZ;
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

                if (modifier.Radius <= 0f || modifier.Strength < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetFalloff(
            in WaveModifierData modifier,
            float x,
            float z,
            out float signedStrength,
            out float offsetX,
            out float offsetZ,
            out float inside)
        {
            signedStrength = 0f;
            offsetX = 0f;
            offsetZ = 0f;
            inside = 0f;

            int sign = GetSign(modifier.Kind);
            if (sign == 0 || modifier.Radius <= 0f)
            {
                return false;
            }

            offsetX = x - modifier.Center.x;
            offsetZ = z - modifier.Center.y;
            float radiusSqr = modifier.Radius * modifier.Radius;
            float distanceSqr = offsetX * offsetX + offsetZ * offsetZ;
            if (distanceSqr >= radiusSqr)
            {
                return false;
            }

            signedStrength = sign * modifier.Strength;
            inside = 1f - distanceSqr / radiusSqr;
            return true;
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
    }
}
