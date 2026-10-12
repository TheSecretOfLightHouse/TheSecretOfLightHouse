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
        private JobHandle _handle;
        private bool _isDisposed;

        public WaterSampler(int shipCapacity, int driftCapacity)
        {
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

            var job = new WaterSampleJob
            {
                Points = _points,
                Waves = _waves,
                Modifiers = _modifiers,
                Heights = _heights,
                WaveCount = waveCount,
                ModifierCount = modifierCount,
                SampleTime = time
            };

            _handle = job.Schedule(_slots.ActiveLength, BatchSize);
            return _handle;
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
