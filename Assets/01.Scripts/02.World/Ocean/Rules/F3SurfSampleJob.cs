using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    // Physics contact and particle emission stay on the main thread; only the shared water math runs here.
    [BurstCompile]
    internal struct F3SurfSampleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<WaveParams> Waves;
        [ReadOnly] public NativeArray<WaveModifierData> Modifiers;
        [ReadOnly] public NativeArray<Vector4> Cells;
        [ReadOnly] public NativeArray<Vector4> Geometry;
        [ReadOnly] public NativeArray<Vector4> Regions;
        [ReadOnly] public NativeArray<ushort> Directions;
        [ReadOnly] public NativeArray<Vector4> DirectionInfo;
        [ReadOnly] public NativeArray<Vector4> PathBounds;
        [WriteOnly] public NativeArray<Vector4> Points;
        [WriteOnly] public NativeArray<Vector4> Ground;
        public Vector4 Layout;
        public int Size;
        public float Extent;
        public int WaveCount;
        public int ModifierCount;
        public int Primary;
        public int AcrossCount;
        public int FirstBand;
        public int Region;
        public float AcrossStep;
        public float CenterAcross;
        public float Time;
        public float BreakDepth;
        private const float BendLimit = 1.3f;

        public void Execute(int index)
        {
            WaveParams wave = Waves[Primary];
            Vector2 direction = wave.Direction;
            Vector2 acrossDirection = new Vector2(-direction.y, direction.x);
            int band = index / AcrossCount;
            int across = index % AcrossCount;
            float side = CenterAcross + (across - (AcrossCount - 1) * 0.5f) * AcrossStep;
            float spectralPhase = F3WaveField.PhaseOffset(wave.Wavelength) / (2f * Mathf.PI);
            float incoming = wave.Speed * Time + (FirstBand + band + 0.25f - spectralPhase) * wave.Wavelength;
            Vector4 ownRegion = Regions[Region];
            Vector2 ownCenter = new Vector2(ownRegion.x, ownRegion.y) + Vector2.one * (Extent * 0.5f);
            float sideRadius = Extent * 0.5f * (Mathf.Abs(acrossDirection.x) + Mathf.Abs(acrossDirection.y));
            float alongRadius = Extent * 0.5f * (Mathf.Abs(direction.x) + Mathf.Abs(direction.y));
            bool canReach = Mathf.Abs(side - Vector2.Dot(ownCenter, acrossDirection)) <= sideRadius;
            if (canReach && PathBounds.IsCreated)
            {
                float reach = BendLimit;
                for (int i = 0; i < Regions.Length; i++)
                {
                    Vector2 center = new Vector2(Regions[i].x, Regions[i].y) + Vector2.one * (Extent * 0.5f);
                    if (Mathf.Abs(side - Vector2.Dot(center, acrossDirection)) <= sideRadius)
                    {
                        Vector4 bound = PathBounds[i];
                        reach += Mathf.Abs(direction.x) * bound.x + Mathf.Abs(direction.y) * bound.y
                            + Mathf.Abs(wave.Speed) * bound.z;
                    }
                }
                canReach = Mathf.Abs(incoming - Vector2.Dot(ownCenter, direction)) <= alongRadius + reach;
            }
            if (!canReach)
            {
                Points[index] = default;
                Ground[index] = default;
                return;
            }
            float along = incoming;
            Vector2 point = direction * along + acrossDirection * side;
            float cheapAlong = along;
            Vector2 cheapPoint = point;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                Vector4 path = Field(cheapPoint, direction);
                float offset = Vector2.Dot(direction, new Vector2(path.y, path.z)) + wave.Speed * path.w;
                cheapAlong = Mathf.Lerp(cheapAlong, incoming - offset, 0.75f);
                cheapPoint = direction * cheapAlong + acrossDirection * side;
            }
            Vector2 slack = direction * BendLimit;
            bool far = Outside(cheapPoint) && Outside(cheapPoint + slack) && Outside(cheapPoint - slack);
            if (far)
            {
                Points[index] = new Vector4(cheapPoint.x, cheapPoint.y, 0f, 0f);
                Ground[index] = default;
                return;
            }
            for (int iteration = 0; iteration < 3; iteration++)
            {
                Vector4 path = Field(point, direction);
                float offset = Vector2.Dot(direction, new Vector2(path.y, path.z)) + wave.Speed * path.w;
                float slope = 0f;
                for (int otherIndex = 0; otherIndex < WaveCount; otherIndex++)
                {
                    if (otherIndex == Primary)
                    {
                        continue;
                    }
                    WaveParams other = Waves[otherIndex];
                    if (other.Wavelength <= 0f)
                    {
                        continue;
                    }
                    Vector4 otherPath = Field(point, other.Direction);
                    float k = 2f * Mathf.PI / other.Wavelength;
                    float phase = k * (Vector2.Dot(other.Direction, point + new Vector2(otherPath.y, otherPath.z))
                        - other.Speed * (Time - otherPath.w)) + F3WaveField.PhaseOffset(other.Wavelength);
                    slope += other.Amplitude * F3WaveField.WavelengthTransmission(otherPath.x, other.Wavelength)
                        * k * Vector2.Dot(other.Direction, direction) * Mathf.Cos(phase);
                }
                float primaryK = 2f * Mathf.PI / wave.Wavelength;
                float bend = BendLimit * (2f / Mathf.PI) * Mathf.Atan(slope
                    / Mathf.Max(0.01f, wave.Amplitude * primaryK * primaryK) / BendLimit);
                along = Mathf.Lerp(along, incoming - offset + bend, 0.75f);
                point = direction * along + acrossDirection * side;
            }
            Vector4 geometry = GeometryAt(point);
            if (Outside(point, geometry))
            {
                Points[index] = new Vector4(point.x, point.y, 0f, 0f);
                Ground[index] = geometry;
                return;
            }
            float height = F3WaveField.Height(Waves.AsReadOnlySpan().Slice(0, WaveCount),
                Modifiers.AsReadOnlySpan().Slice(0, ModifierCount), Cells, Layout, Size, point.x, point.y, Time,
                Geometry, Size, Regions, Extent, Directions, DirectionInfo);
            Points[index] = new Vector4(point.x, point.y, height, 1f);
            Ground[index] = geometry;
        }

        private Vector4 Field(Vector2 point, Vector2 direction)
        {
            return F3WaveField.SampleDirection(Directions, DirectionInfo, Regions, Extent, Layout.z,
                point.x, point.y, direction, default);
        }

        private Vector4 GeometryAt(Vector2 point)
        {
            return F3WaveField.GeometryAt(Geometry, Layout, Size, point.x, point.y, Regions, Extent);
        }

        private bool Outside(Vector2 point)
        {
            return Outside(point, GeometryAt(point));
        }

        private bool Outside(Vector2 point, Vector4 geometry)
        {
            return geometry.x <= 0.02f || geometry.x >= BreakDepth || geometry.y <= 0f
                || F3WaveField.FindRegion(point.x, point.y, Regions, Extent) != Region;
        }
    }
}
