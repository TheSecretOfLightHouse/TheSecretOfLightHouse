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
        [WriteOnly] public NativeArray<float> Heights;
        public int WaveCount;
        public int ModifierCount;
        public float SampleTime;

        public void Execute(int index)
        {
            ReadOnlySpan<WaveParams> waves = Waves.AsReadOnlySpan().Slice(0, WaveCount);
            ReadOnlySpan<WaveModifierData> modifiers = Modifiers.AsReadOnlySpan().Slice(0, ModifierCount);
            Vector3 point = Points[index];
            Heights[index] = WaveModel.HeightAt(waves, modifiers, point.x, point.z, SampleTime);
        }
    }
}
