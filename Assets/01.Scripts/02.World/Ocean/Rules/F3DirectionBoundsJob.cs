using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [BurstCompile]
    internal struct F3DirectionBoundsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ushort> Data;
        [ReadOnly] public NativeArray<Vector4> Info;
        [WriteOnly] public NativeArray<Vector4> Bounds;

        public void Execute(int index)
        {
            Vector4 info = Info[index];
            int channels = (int)info.w * (int)info.w * 4;
            int first = (int)info.x * channels;
            int end = first + (int)info.y * channels;
            Vector4 result = Vector4.zero;
            for (int i = first; i < end; i += 4)
            {
                result.x = math.max(result.x, math.abs(math.f16tof32(Data[i + 1])));
                result.y = math.max(result.y, math.abs(math.f16tof32(Data[i + 2])));
                result.z = math.max(result.z, math.abs(math.f16tof32(Data[i + 3])));
            }
            Bounds[index] = result;
        }
    }
}
