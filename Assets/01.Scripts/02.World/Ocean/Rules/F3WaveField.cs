using System;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class F3WaveField
    {
        private const float DepthAmplitudeScale = 1.25f;
        private const float ShortWavelength = 8f;
        private const float LongWavelength = 60f;
        private const float ShortExponent = 1.5f;
        private const float LongExponent = 0.5f;
        public static NativeArray<Vector4> Cells { get; private set; }

        public static NativeArray<Vector4> Geometry { get; private set; }

        public static Vector4 Layout { get; private set; }

        public static NativeArray<Vector4> Regions { get; private set; }
        public static float RegionExtent { get; private set; }

        public static int Size { get; private set; }

        public static int GeometrySize { get; private set; }

        public static bool IsActive => Cells.IsCreated && Geometry.IsCreated && Size > 1;
        public static void Register(NativeArray<Vector4> cells, Vector4 layout, int size, NativeArray<Vector4> geometry, int geometrySize, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            Regions = regions;
            RegionExtent = regionExtent;
            Cells = cells;
            Layout = layout;
            Size = size;
            Geometry = geometry;
            GeometrySize = geometrySize;
        }

        public static void Unregister(NativeArray<Vector4> cells)
        {
            if (Cells.Equals(cells))
            {
                Regions = default;
                RegionExtent = 0f;
                Cells = default;
                Geometry = default;
                Size = 0;
                GeometrySize = 0;
            }
        }

        public static Vector4 GeometryAt(NativeArray<Vector4> geometry, Vector4 layout, int size, float x, float z, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            if (!MapPoint(ref x, ref z, regions, regionExtent))
            {
                return new Vector4(4f, 1f, 1f, 4f);
            }
            if (size < 2)
            {
                return new Vector4(4f, 1f, 1f, 4f);
            }
            float u = (x - layout.x) / layout.w * size - 0.5f;
            float v = (z - layout.y) / layout.w * size - 0.5f;
            if (u < 0f || v < 0f || u > size - 1 || v > size - 1)
            {
                return new Vector4(4f, 1f, 1f, 4f);
            }
            int nearestX = Mathf.Clamp(Mathf.FloorToInt(u + 0.5f), 0, size - 1);
            int nearestZ = Mathf.Clamp(Mathf.FloorToInt(v + 0.5f), 0, size - 1);
            int ix = Mathf.Min((int)u, size - 2);
            int iz = Mathf.Min((int)v, size - 2);
            Vector4 filtered = Vector4.Lerp(
                Vector4.Lerp(geometry[iz * size + ix], geometry[iz * size + ix + 1], u - ix),
                Vector4.Lerp(geometry[(iz + 1) * size + ix], geometry[(iz + 1) * size + ix + 1], u - ix), v - iz);
            Vector4 result = geometry[nearestZ * size + nearestX];
            result.x = filtered.x;
            result.w = filtered.w;
            return result;
        }

        public static Vector4 SampleRaw(NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, Vector4 mask, int geometrySize, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            if (!MapPoint(ref x, ref z, regions, regionExtent))
            {
                return new Vector4(1f, 0f, 0f, 0f);
            }
            if (mask.w <= 0f || mask.y <= 0f)
            {
                return Vector4.zero;
            }

            float u = (x - layout.x) / layout.z - 0.5f;
            float v = (z - layout.y) / layout.z - 0.5f;
            if (size < 2 || u < 0f || v < 0f || u > size - 1 || v > size - 1)
            {
                return new Vector4(1f, 0f, 0f, 0f);
            }

            int nearestX = Mathf.Clamp(Mathf.FloorToInt(u + 0.5f), 0, size - 1);
            int nearestZ = Mathf.Clamp(Mathf.FloorToInt(v + 0.5f), 0, size - 1);
            if (geometrySize < 2 && cells[nearestZ * size + nearestX].x <= 0f)
            {
                return Vector4.zero;
            }

            int ix = Mathf.Min((int)u, size - 2);
            int iz = Mathf.Min((int)v, size - 2);
            return Vector4.Lerp(Vector4.Lerp(cells[iz * size + ix], cells[iz * size + ix + 1], u - ix), Vector4.Lerp(cells[(iz + 1) * size + ix], cells[(iz + 1) * size + ix + 1], u - ix), v - iz);
        }

        // Map only the field lookup; wave phase remains in world metres.
        public static int FindRegion(float x, float z, NativeArray<Vector4> regions, float extent)
        {
            int selected = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < regions.Length; i++)
            {
                Vector4 region = regions[i];
                float localX = x - region.x;
                float localZ = z - region.y;
                float distance = (localX - extent * 0.5f) * (localX - extent * 0.5f)
                    + (localZ - extent * 0.5f) * (localZ - extent * 0.5f);
                if (localX >= 0f && localZ >= 0f && localX < extent && localZ < extent && distance < nearest)
                {
                    nearest = distance;
                    selected = i;
                }
            }
            return selected;
        }

        public static bool MapPoint(ref float x, ref float z, NativeArray<Vector4> regions, float extent)
        {
            if (extent <= 0f)
            {
                return true;
            }
            int selected = FindRegion(x, z, regions, extent);
            if (selected < 0)
            {
                return false;
            }
            Vector4 chosen = regions[selected];
            x += chosen.z - chosen.x;
            z += chosen.w - chosen.y;
            return true;
        }

        public static float BreakingLimit(float groundDepth)
        {
            return Mathf.Max(0.25f, groundDepth + 0.05f) * DepthAmplitudeScale;
        }

        // Long swell diffracts around islands (exponent < 1 keeps more), short chop is blocked (exponent > 1 keeps less).
        public static float WavelengthTransmission(float baked, float wavelength)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((wavelength - ShortWavelength) / (LongWavelength - ShortWavelength)));
            float exponent = Mathf.Lerp(ShortExponent, LongExponent, t);
            return Mathf.Pow(Mathf.Max(baked, 0f), exponent);
        }

        public static Vector4 Sample(NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, NativeArray<Vector4> geometry = default, int geometrySize = 0, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            Vector4 mask = GeometryAt(geometry, layout, geometrySize, x, z, regions, regionExtent);
            Vector4 field = SampleRaw(cells, layout, size, x, z, mask, geometrySize, regions, regionExtent);
            if (mask.w <= 0f || mask.y <= 0f || !IsInsideCells(layout, size, x, z, regions, regionExtent))
            {
                return field;
            }

            // Shallow breaking limits height without stopping the incoming phase metres before the coast.
            field.x = Mathf.Min(field.x, BreakingLimit(mask.x));
            return field;
        }

        private static bool IsInsideCells(Vector4 layout, int size, float x, float z, NativeArray<Vector4> regions, float regionExtent)
        {
            if (!MapPoint(ref x, ref z, regions, regionExtent))
            {
                return false;
            }
            float u = (x - layout.x) / layout.z - 0.5f;
            float v = (z - layout.y) / layout.z - 0.5f;
            return size >= 2 && u >= 0f && v >= 0f && u <= size - 1 && v <= size - 1;
        }

        public static Vector3 Displacement(ReadOnlySpan<WaveParams> waves, NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, float time, NativeArray<Vector4> geometry = default, int geometrySize = 0, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            Vector4 ground = GeometryAt(geometry, layout, geometrySize, x, z, regions, regionExtent);
            Vector4 field = SampleRaw(cells, layout, size, x, z, ground, geometrySize, regions, regionExtent);
            float breakingLimit = BreakingLimit(ground.x);
            float depth = Mathf.Max(0.25f, ground.x + 0.05f);
            float shoal = (1f - Mathf.SmoothStep(0f, 1f, (depth - 0.6f) / 1.9f))
                * Mathf.SmoothStep(0f, 1f, (depth - 0.08f) / 0.27f);
            float swell = OpenSeaSwell.FactorAt(x, z, time);
            Vector3 displacement = Vector3.zero;
            for (int i = 0; i < waves.Length; i++)
            {
                ref readonly WaveParams wave = ref waves[i];
                if (wave.Wavelength <= 0f)
                {
                    continue;
                }

                float k = 2f * Mathf.PI / wave.Wavelength;
                // Retain the original incoming phase and clock across the delayed water path.
                float phase = k * (wave.Direction.x * (x + field.y) + wave.Direction.y * (z + field.z) - wave.Speed * (time - field.w));
                float transmission = Mathf.Min(WavelengthTransmission(field.x, wave.Wavelength) * swell, breakingLimit);
                float horizontal = transmission * wave.Steepness / k * Mathf.Cos(phase);
                displacement.x += wave.Direction.x * horizontal;
                displacement.z += wave.Direction.y * horizontal;
                displacement.y += transmission * wave.Amplitude * (1f + 0.65f * shoal) * (Mathf.Sin(phase) - 0.25f * shoal * Mathf.Cos(phase * 2f));
            }

            return displacement;
        }

        public static float Height(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, float time, NativeArray<Vector4> geometry = default, int geometrySize = 0, NativeArray<Vector4> regions = default, float regionExtent = 0f)
        {
            Vector4 mask = GeometryAt(geometry, layout, geometrySize, x, z, regions, regionExtent);
            if (mask.w <= 0f || mask.y <= 0f)
            {
                return 0f;
            }

            float restX = x;
            float restZ = z;
            for (int i = 0; i < WaveModel.InverseIterations; i++)
            {
                Vector3 offset = Displacement(waves, cells, layout, size, restX, restZ, time, geometry, geometrySize, regions, regionExtent);
                restX = x - offset.x;
                restZ = z - offset.z;
            }

            return Displacement(waves, cells, layout, size, restX, restZ, time, geometry, geometrySize, regions, regionExtent).y + WaveModifierModel.HeightAt(modifiers, x, z, time);
        }

        public static float HeightAt(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, float x, float z, float time)
        {
            return IsActive ? Height(waves, modifiers, Cells, Layout, Size, x, z, time, Geometry, GeometrySize, Regions, RegionExtent) : WaveModel.HeightAt(waves, modifiers, x, z, time);
        }
    }
}
