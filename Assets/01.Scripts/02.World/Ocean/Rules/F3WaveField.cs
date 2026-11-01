using System;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class F3WaveField
    {
        // Wave height may not exceed this share of the water depth. Shallow water near islands keeps it low so the
        // surf stays below the beach; it opens up to the deep value by 8 m of depth. Mirrored in F3Terrain.hlsl.
        private const float ShoreAmplitudeScale = 0.5f;
        private const float DepthAmplitudeScale = 1.25f;
        private const float ShoalGain = 0.2f;
        private const float ShortWavelength = 8f;
        private const float LongWavelength = 60f;
        private const float ShortExponent = 1.5f;
        private const float LongExponent = 0.5f;
        public static NativeArray<Vector4> Cells { get; private set; }

        public static NativeArray<Vector4> Geometry { get; private set; }

        public static Vector4 Layout { get; private set; }

        public static NativeArray<Vector4> Regions { get; private set; }
        public static float RegionExtent { get; private set; }
        public static NativeArray<ushort> DirectionData { get; private set; }
        public static NativeArray<Vector4> DirectionInfo { get; private set; }
        public static NativeArray<Vector4> DirectionBounds { get; private set; }

        public static void RegisterDirections(NativeArray<ushort> data, NativeArray<Vector4> info, NativeArray<Vector4> bounds = default)
        {
            DirectionData = data;
            DirectionInfo = info;
            DirectionBounds = bounds;
        }

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
                DirectionData = default;
                DirectionInfo = default;
                DirectionBounds = default;
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
            float depth = Mathf.Max(0.25f, groundDepth + 0.05f);
            return depth * Mathf.Lerp(ShoreAmplitudeScale, DepthAmplitudeScale, Mathf.SmoothStep(0f, 1f, (depth - 2f) / 6f));
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

        public static Vector3 Displacement(ReadOnlySpan<WaveParams> waves, NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, float time, NativeArray<Vector4> geometry = default, int geometrySize = 0, NativeArray<Vector4> regions = default, float regionExtent = 0f, NativeArray<ushort> directionData = default, NativeArray<Vector4> directionInfo = default)
        {
            Vector4 ground = GeometryAt(geometry, layout, geometrySize, x, z, regions, regionExtent);
            Vector4 fallback = directionData.IsCreated && directionInfo.IsCreated && regionExtent > 0f
                ? default : SampleRaw(cells, layout, size, x, z, ground, geometrySize, regions, regionExtent);
            float breakingLimit = BreakingLimit(ground.x);
            float depth = Mathf.Max(0.25f, ground.x + 0.05f);
            float shoal = (1f - Mathf.SmoothStep(0f, 1f, (depth - 0.6f) / 1.9f))
                * Mathf.SmoothStep(0f, 1f, (depth - 0.08f) / 0.27f);
            Vector3 displacement = Vector3.zero;
            float envelope = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                ref readonly WaveParams wave = ref waves[i];
                if (wave.Wavelength <= 0f)
                {
                    continue;
                }

                float k = 2f * Mathf.PI / wave.Wavelength;
                Vector4 field = SampleDirection(directionData, directionInfo, regions, regionExtent, layout.z, x, z, wave.Direction, fallback);
                if (ground.w <= 0f || ground.y <= 0f) field = Vector4.zero;
                float swell = OpenSeaSwell.FactorAt(x + wave.Direction.y * wave.Wavelength, z - wave.Direction.x * wave.Wavelength, time);
                // Retain the original incoming phase and clock across the delayed water path.
                float phase = k * (wave.Direction.x * (x + field.y) + wave.Direction.y * (z + field.z) - wave.Speed * (time - field.w)) + PhaseOffset(wave.Wavelength);
                float transmission = Mathf.Min(WavelengthTransmission(field.x, wave.Wavelength) * swell, breakingLimit);
                float horizontal = transmission * wave.Steepness / k * Mathf.Cos(phase);
                displacement.x += wave.Direction.x * horizontal;
                displacement.z += wave.Direction.y * horizontal;
                displacement.y += transmission * wave.Amplitude * (1f + ShoalGain * shoal) * (Mathf.Sin(phase) - 0.25f * shoal * Mathf.Cos(phase * 2f));
                envelope += transmission * wave.Amplitude * (1f + ShoalGain * shoal) * (1f + 0.25f * shoal);
            }

            // Bound the combined height in metres, rather than treating the depth limit as an amplitude multiplier.
            return displacement * Mathf.Min(1f, breakingLimit / Mathf.Max(envelope, 0.0001f));
        }

        public static float PhaseOffset(float wavelength)
        {
            // A fixed spectral phase avoids every component sharing a crest at the world origin.
            return wavelength * 2.39996323f;
        }

        public static float Height(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, NativeArray<Vector4> cells, Vector4 layout, int size, float x, float z, float time, NativeArray<Vector4> geometry = default, int geometrySize = 0, NativeArray<Vector4> regions = default, float regionExtent = 0f, NativeArray<ushort> directionData = default, NativeArray<Vector4> directionInfo = default)
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
                Vector3 offset = Displacement(waves, cells, layout, size, restX, restZ, time, geometry, geometrySize, regions, regionExtent, directionData, directionInfo);
                restX = x - offset.x;
                restZ = z - offset.z;
            }

            return Displacement(waves, cells, layout, size, restX, restZ, time, geometry, geometrySize, regions, regionExtent, directionData, directionInfo).y + WaveModifierModel.HeightAt(modifiers, x, z, time);
        }

        public static float HeightAt(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, float x, float z, float time)
        {
            return IsActive ? Height(waves, modifiers, Cells, Layout, Size, x, z, time, Geometry, GeometrySize, Regions, RegionExtent, DirectionData, DirectionInfo) : WaveModel.HeightAt(waves, modifiers, x, z, time);
        }

        public static Vector4 SampleDirection(NativeArray<ushort> data, NativeArray<Vector4> info, NativeArray<Vector4> regions,
            float extent, float cell, float x, float z, Vector2 direction, Vector4 fallback)
        {
            if (!data.IsCreated || !info.IsCreated || extent <= 0f) return fallback;
            Vector4 result = new Vector4(1f, 0f, 0f, 0f);
            for (int i = 0; i < regions.Length; i++)
            {
                Vector4 region = regions[i];
                float localX = x - region.x;
                float localZ = z - region.y;
                float edge = Mathf.Min(Mathf.Min(localX, localZ), Mathf.Min(extent - localX, extent - localZ));
                if (edge <= 0f) continue;
                Vector4 field = F3DirectionalTransmission.Sample(data, info[i], localX / cell - 0.5f, localZ / cell - 0.5f, direction);
                float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / (2f * cell)));
                // Open-water tiles contribute identity; overlapping island shadows accumulate without an ownership seam.
                result.x *= Mathf.Lerp(1f, field.x, weight);
                result.y += field.y * weight;
                result.z += field.z * weight;
                result.w += field.w * weight;
            }
            return result;
        }

        public static Vector4 DirectionAt(float x, float z, Vector2 direction)
        {
            if (DirectionData.IsCreated && DirectionInfo.IsCreated && RegionExtent > 0f)
            {
                return SampleDirection(DirectionData, DirectionInfo, Regions, RegionExtent, Layout.z, x, z, direction, default);
            }

            Vector4 fallback = Sample(Cells, Layout, Size, x, z, Geometry, GeometrySize, Regions, RegionExtent);
            return SampleDirection(DirectionData, DirectionInfo, Regions, RegionExtent, Layout.z, x, z, direction, fallback);
        }

        public static Vector3 NormalAt(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, float x, float z, float time)
        {
            if (!IsActive)
            {
                return WaveModel.NormalAt(waves, modifiers, x, z, time);
            }
            const float Step = 0.1f;
            float dx = HeightAt(waves, modifiers, x + Step, z, time) - HeightAt(waves, modifiers, x - Step, z, time);
            float dz = HeightAt(waves, modifiers, x, z + Step, time) - HeightAt(waves, modifiers, x, z - Step, time);
            return new Vector3(-dx, 2f * Step, -dz).normalized;
        }
    }
}
