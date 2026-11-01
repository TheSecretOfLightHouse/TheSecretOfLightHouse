using System;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    // Binary layout: Magic, Size, Count, BaseAngleDegrees (16 bytes), then Count slices of Size*Size RGBAHalf cells.
    public static class F3DirectionalTransmission
    {
        public const int Magic = 0x33444633;
        public const int HeaderBytes = 16;
        public const int ChannelsPerCell = 4;
        public const int BytesPerCell = ChannelsPerCell * sizeof(ushort);
        private const int ExponentCount = 32;
        private const int ExponentMask = 0x1F;
        private const int MantissaBits = 10;
        private const int MantissaMask = 0x3FF;
        private const int ImplicitMantissa = 1 << MantissaBits;
        private const int SignMask = 0x8000;
        private const int ExponentBias = 25;
        private const int SubnormalExponent = -24;
        private const float FullTurnDegrees = 360f;
        private static readonly float[] ExponentScales = BuildExponentScales();

        // Metadata: first array slice, direction count, base angle, tile resolution.
        public static Vector4 Sample(NativeArray<ushort> data, Vector4 info, float u, float v, Vector2 direction)
        {
            int size = (int)info.w;
            PickSlices(AngleOf(direction), info.z, (int)info.y, out int first, out int second, out float weight);
            return Vector4.Lerp(SampleSlice(data, ((int)info.x + first) * size * size, size, u, v),
                SampleSlice(data, ((int)info.x + second) * size * size, size, u, v), weight);
        }

        private static Vector4 SampleSlice(NativeArray<ushort> data, int offset, int size, float u, float v)
        {
            u = Mathf.Clamp(u, 0f, size - 1f);
            v = Mathf.Clamp(v, 0f, size - 1f);
            int x = Mathf.Min((int)u, size - 2);
            int z = Mathf.Min((int)v, size - 2);
            return Vector4.Lerp(Vector4.Lerp(ReadCell(data, offset + z * size + x), ReadCell(data, offset + z * size + x + 1), u - x),
                Vector4.Lerp(ReadCell(data, offset + (z + 1) * size + x), ReadCell(data, offset + (z + 1) * size + x + 1), u - x), v - z);
        }

        private static Vector4 ReadCell(NativeArray<ushort> data, int cell)
        {
            int channel = cell * ChannelsPerCell;
            return new Vector4(Unity.Mathematics.math.f16tof32(data[channel]), Unity.Mathematics.math.f16tof32(data[channel + 1]),
                Unity.Mathematics.math.f16tof32(data[channel + 2]), Unity.Mathematics.math.f16tof32(data[channel + 3]));
        }

        public static float AngleOf(Vector2 travelDirection)
        {
            return Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;
        }

        public static Vector2 DirectionOf(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static bool TryReadHeader(NativeArray<byte> bytes, out int size, out int count, out float baseAngleDegrees)
        {
            size = 0;
            count = 0;
            baseAngleDegrees = 0f;
            if (!bytes.IsCreated || bytes.Length < HeaderBytes)
            {
                return false;
            }

            NativeArray<int> words = bytes.GetSubArray(0, HeaderBytes).Reinterpret<int>(sizeof(byte));
            size = words[1];
            count = words[2];
            baseAngleDegrees = BitConverter.Int32BitsToSingle(words[3]);
            long expected = HeaderBytes + (long)count * size * size * BytesPerCell;
            return words[0] == Magic && size > 1 && count > 1 && bytes.Length == expected;
        }

        public static void PickSlices(float angleDegrees, float baseAngleDegrees, int count, out int first, out int second, out float weight)
        {
            float relative = Mathf.Repeat(angleDegrees - baseAngleDegrees, FullTurnDegrees) / (FullTurnDegrees / count);
            int lower = Mathf.FloorToInt(relative);
            weight = relative - lower;
            first = lower % count;
            second = (first + 1) % count;
        }

        public static float HalfToFloat(ushort bits)
        {
            int exponent = (bits >> MantissaBits) & ExponentMask;
            int mantissa = bits & MantissaMask;
            float magnitude = exponent == 0 ? mantissa * ExponentScales[0] : (mantissa + ImplicitMantissa) * ExponentScales[exponent];
            return (bits & SignMask) != 0 ? -magnitude : magnitude;
        }

        private static float[] BuildExponentScales()
        {
            var scales = new float[ExponentCount];
            scales[0] = (float)Math.Pow(2.0, SubnormalExponent);
            for (int exponent = 1; exponent < ExponentCount - 1; exponent++)
            {
                scales[exponent] = (float)Math.Pow(2.0, exponent - ExponentBias);
            }

            return scales;
        }
    }
}
