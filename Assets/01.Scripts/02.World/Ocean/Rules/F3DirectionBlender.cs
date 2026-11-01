using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    // Blends two baked direction slices into a destination field, a bounded number of cells per call.
    public sealed class F3DirectionBlender
    {
        private readonly NativeArray<ushort> _halves;
        private readonly int _cellCount;
        private int _firstOffset;
        private int _secondOffset;
        private float _weight;
        private int _cursor;

        public F3DirectionBlender(NativeArray<byte> bytes, int size, int count)
        {
            _cellCount = size * size;
            int payload = count * _cellCount * F3DirectionalTransmission.BytesPerCell;
            _halves = bytes.GetSubArray(F3DirectionalTransmission.HeaderBytes, payload).Reinterpret<ushort>(sizeof(byte));
            _cursor = _cellCount;
        }

        public int CellCount => _cellCount;

        public bool IsBusy => _cursor < _cellCount;

        public void Begin(int first, int second, float weight)
        {
            int stride = _cellCount * F3DirectionalTransmission.ChannelsPerCell;
            _firstOffset = first * stride;
            _secondOffset = second * stride;
            _weight = weight;
            _cursor = 0;
        }

        public bool Step(NativeArray<Vector4> destination, int budget)
        {
            int end = (int)System.Math.Min((long)_cursor + budget, _cellCount);
            for (int cell = _cursor; cell < end; cell++)
            {
                int channel = cell * F3DirectionalTransmission.ChannelsPerCell;
                destination[cell] = Read(_firstOffset + channel, _secondOffset + channel);
            }

            _cursor = end;
            return !IsBusy;
        }

        private Vector4 Read(int first, int second)
        {
            return new Vector4(
                Mix(first, second),
                Mix(first + 1, second + 1),
                Mix(first + 2, second + 2),
                Mix(first + 3, second + 3));
        }

        private float Mix(int first, int second)
        {
            float a = F3DirectionalTransmission.HalfToFloat(_halves[first]);
            return _weight <= 0f ? a : Mathf.Lerp(a, F3DirectionalTransmission.HalfToFloat(_halves[second]), _weight);
        }
    }
}
