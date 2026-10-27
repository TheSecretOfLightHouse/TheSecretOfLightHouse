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
        [WriteOnly] public NativeArray<float> Heights;
        public int WaveCount;
        public int ModifierCount;
        public float SampleTime;

        public void Execute(int index)
        {
            ReadOnlySpan<WaveParams> waves = Waves.AsReadOnlySpan().Slice(0, WaveCount);
            ReadOnlySpan<WaveModifierData> modifiers = Modifiers.AsReadOnlySpan().Slice(0, ModifierCount);
            Vector3 point = Points[index];
            Heights[index] = WaveModel.HeightAt(waves, modifiers, Coasts.AsReadOnlySpan(), CoastEdges.AsReadOnlySpan(), CoastProfiles.AsReadOnlySpan(), point.x, point.z, SampleTime);
        }
    }
}
