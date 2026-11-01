using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public class WaterSampler : IDisposable
    {
        private const int BatchSize = 16;

        private readonly SamplerSlotAllocator _slots;
        private NativeArray<Vector3> _points;
        private NativeArray<float> _heights;
        private NativeArray<WaveParams> _waves;
        private NativeArray<WaveModifierData> _modifiers;
        private NativeArray<Vector4> _coasts;
        private NativeArray<Vector4> _edges;
        private NativeArray<Vector4> _profiles;
        private NativeArray<Vector4> _lagoons;
        private int _coastVersion = -1;
        private JobHandle _handle;
        private NativeArray<Vector4> _f3Fallback;
        private NativeArray<Vector4> _f3GeometryFallback;
        private NativeArray<ushort> _f3DirectionFallback;
        private bool _isDisposed;
        private NativeArray<Vector3> _queryPoints;
        private NativeArray<float> _queryHeights;

        public WaterSampler(int shipCapacity, int driftCapacity)
        {
            _queryPoints = new NativeArray<Vector3>(4, Allocator.Persistent);
            _queryHeights = new NativeArray<float>(4, Allocator.Persistent);
            _f3Fallback = new NativeArray<Vector4>(1, Allocator.Persistent);
            _f3DirectionFallback = new NativeArray<ushort>(1, Allocator.Persistent);
            _f3GeometryFallback = new NativeArray<Vector4>(1, Allocator.Persistent);
            _profiles = new NativeArray<Vector4>(CoastWaveRegistry.Capacity, Allocator.Persistent);
            _lagoons = new NativeArray<Vector4>(CoastWaveRegistry.Capacity, Allocator.Persistent);
            _coasts = new NativeArray<Vector4>(CoastWaveRegistry.Capacity, Allocator.Persistent);
            _edges = new NativeArray<Vector4>(CoastWaveRegistry.Capacity * CoastWaveRegistry.Segments, Allocator.Persistent);
            _slots = new SamplerSlotAllocator(shipCapacity, driftCapacity);
            _points = new NativeArray<Vector3>(_slots.Capacity, Allocator.Persistent);
            _heights = new NativeArray<float>(_slots.Capacity, Allocator.Persistent);
            _waves = new NativeArray<WaveParams>(WaveModel.MaxWaves, Allocator.Persistent);
            _modifiers = new NativeArray<WaveModifierData>(WaveModifierModel.MaxModifiers, Allocator.Persistent);
        }

        public bool TryAllocate(SamplerPool pool, int count, out SamplerTicket ticket)
        {
            if (_isDisposed)
            {
                ticket = default;
                return false;
            }

            return _slots.TryAllocate(pool, count, out ticket);
        }

        public void Release(in SamplerTicket ticket)
        {
            if (_isDisposed)
            {
                return;
            }

            _handle.Complete();

            for (int i = 0; i < ticket.Count; i++)
            {
                int slot = ticket.Start + i;
                if (slot >= 0 && slot < _heights.Length)
                {
                    _heights[slot] = 0f;
                }
            }

            _slots.Release(in ticket);
        }

        public void SetPoint(in SamplerTicket ticket, int index, Vector3 worldPos)
        {
            if ((uint)index >= (uint)ticket.Count)
            {
                return;
            }

            _points[ticket.Start + index] = worldPos;
        }

        public float GetHeight(in SamplerTicket ticket, int index)
        {
            if ((uint)index >= (uint)ticket.Count)
            {
                return 0f;
            }

            return _heights[ticket.Start + index];
        }

        public JobHandle Schedule(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float time)
        {
            if (_isDisposed)
            {
                return default;
            }

            _handle.Complete();

            int waveCount = Math.Min(waves.Length, WaveModel.MaxWaves);
            for (int i = 0; i < waveCount; i++)
            {
                _waves[i] = waves[i];
            }

            int modifierCount = Math.Min(modifiers.Length, WaveModifierModel.MaxModifiers);
            for (int i = 0; i < modifierCount; i++)
            {
                _modifiers[i] = modifiers[i];
            }

            if (_coastVersion != CoastWaveRegistry.Version)
            {
                for (int i = 0; i < _coasts.Length; i++)
                {
                    _coasts[i] = CoastWaveRegistry.Bounds[i];
                    _profiles[i] = CoastWaveRegistry.Profiles[i];
                    _lagoons[i] = CoastWaveRegistry.Lagoons[i];
                }
                for (int i = 0; i < _edges.Length; i++)
                {
                    _edges[i] = CoastWaveRegistry.Edges[i];
                }
                _coastVersion = CoastWaveRegistry.Version;
            }

            WaterSampleJob job = CreateJob(_points, _heights, waveCount, modifierCount, time);

            _handle = job.Schedule(_slots.ActiveLength, BatchSize);
            return _handle;
        }

        private WaterSampleJob CreateJob(NativeArray<Vector3> points, NativeArray<float> heights,
            int waveCount, int modifierCount, float time)
        {
            return new WaterSampleJob
            {
                Points = points,
                Terrain = F3WaveField.IsActive ? F3WaveField.Cells : _f3Fallback,
                TerrainLayout = F3WaveField.Layout,
                TerrainRegions = F3WaveField.Regions.IsCreated ? F3WaveField.Regions : _f3Fallback,
                TerrainRegionExtent = F3WaveField.RegionExtent,
                TerrainDirections = F3WaveField.DirectionData.IsCreated ? F3WaveField.DirectionData : _f3DirectionFallback,
                TerrainDirectionInfo = F3WaveField.DirectionInfo.IsCreated ? F3WaveField.DirectionInfo : _f3Fallback,
                TerrainGeometry = F3WaveField.IsActive ? F3WaveField.Geometry : _f3GeometryFallback,
                TerrainGeometrySize = F3WaveField.IsActive ? F3WaveField.GeometrySize : 0,
                TerrainSize = F3WaveField.IsActive ? F3WaveField.Size : 0,
                Coasts = _coasts,
                CoastEdges = _edges,
                CoastProfiles = _profiles,
                CoastLagoons = _lagoons,
                Waves = _waves,
                Modifiers = _modifiers,
                Heights = heights,
                WaveCount = waveCount,
                ModifierCount = modifierCount,
                SampleTime = time
            };

        }

        public float SampleF3Height(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, Vector3 position, float time)
        {
            _queryPoints[0] = position;
            RunImmediate(waves, modifiers, time, 1);
            return _queryHeights[0];
        }

        public Vector3 SampleF3Normal(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, Vector3 position, float time)
        {
            const float Step = 0.1f;
            _queryPoints[0] = position + new Vector3(Step, 0f, 0f);
            _queryPoints[1] = position - new Vector3(Step, 0f, 0f);
            _queryPoints[2] = position + new Vector3(0f, 0f, Step);
            _queryPoints[3] = position - new Vector3(0f, 0f, Step);
            RunImmediate(waves, modifiers, time, 4);
            return new Vector3(-(_queryHeights[0] - _queryHeights[1]), 2f * Step,
                -(_queryHeights[2] - _queryHeights[3])).normalized;
        }

        private void RunImmediate(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers, float time, int count)
        {
            // Immediate queries share the immutable field, but never overlap a scheduled sampler write.
            _handle.Complete();
            int waveCount = Math.Min(waves.Length, WaveModel.MaxWaves);
            int modifierCount = Math.Min(modifiers.Length, WaveModifierModel.MaxModifiers);
            for (int i = 0; i < waveCount; i++)
            {
                _waves[i] = waves[i];
            }
            for (int i = 0; i < modifierCount; i++)
            {
                _modifiers[i] = modifiers[i];
            }
            WaterSampleJob job = CreateJob(_queryPoints, _queryHeights, waveCount, modifierCount, time);
            job.Run(count);
        }

        public void Complete()
        {
            _handle.Complete();
        }

        public void Run(
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers,
            float time)
        {
            Schedule(waves, modifiers, time);
            Complete();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _handle.Complete();
            _isDisposed = true;
            _queryPoints.Dispose();
            _queryHeights.Dispose();
            _f3Fallback.Dispose();
            _f3DirectionFallback.Dispose();
            _f3GeometryFallback.Dispose();
            _coasts.Dispose();
            _edges.Dispose();
            _profiles.Dispose();
            _lagoons.Dispose();

            if (_points.IsCreated)
            {
                _points.Dispose();
            }

            if (_heights.IsCreated)
            {
                _heights.Dispose();
            }

            if (_waves.IsCreated)
            {
                _waves.Dispose();
            }

            if (_modifiers.IsCreated)
            {
                _modifiers.Dispose();
            }
        }
    }
}
