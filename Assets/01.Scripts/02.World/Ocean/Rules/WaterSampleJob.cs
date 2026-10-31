using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [BurstCompile]
    internal struct WaterSampleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> Points;
        [ReadOnly] public NativeArray<WaveParams> Waves;
        [ReadOnly] public NativeArray<WaveModifierData> Modifiers;
        [ReadOnly] public NativeArray<Vector4> Coasts;
        [ReadOnly] public NativeArray<Vector4> CoastEdges;
        [ReadOnly] public NativeArray<Vector4> CoastProfiles;
        [ReadOnly] public NativeArray<Vector4> CoastLagoons;
        [ReadOnly] public NativeArray<Vector4> Terrain;
        [ReadOnly] public NativeArray<Vector4> TerrainGeometry;
        [ReadOnly] public NativeArray<Vector4> TerrainRegions;
        public float TerrainRegionExtent;
        public int TerrainGeometrySize;
        public Vector4 TerrainLayout;
        public int TerrainSize;
        [WriteOnly] public NativeArray<float> Heights;
        public int WaveCount;
        public int ModifierCount;
        public float SampleTime;

        public void Execute(int index)
        {
            ReadOnlySpan<WaveParams> waves = Waves.AsReadOnlySpan().Slice(0, WaveCount);
            ReadOnlySpan<WaveModifierData> modifiers = Modifiers.AsReadOnlySpan().Slice(0, ModifierCount);
            Vector3 point = Points[index];
            Heights[index] = TerrainSize > 1
                ? F3WaveField.Height(waves, modifiers, Terrain, TerrainLayout, TerrainSize, point.x, point.z, SampleTime, TerrainGeometry, TerrainGeometrySize, TerrainRegions, TerrainRegionExtent)
                : WaveModel.HeightAt(waves, modifiers, Coasts.AsReadOnlySpan(), CoastEdges.AsReadOnlySpan(), CoastProfiles.AsReadOnlySpan(), CoastLagoons.AsReadOnlySpan(), point.x, point.z, SampleTime);
        }
    }
}
