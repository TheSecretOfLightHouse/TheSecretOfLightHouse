using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class StableHash
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;
        private const float FloatScale = 100f;

        private uint _value = OffsetBasis;

        public uint Value => _value;

        public void Add(int value)
        {
            AddByte((byte)value);
            AddByte((byte)(value >> 8));
            AddByte((byte)(value >> 16));
            AddByte((byte)(value >> 24));
        }

        public void Add(float value)
        {
            Add(Mathf.RoundToInt(value * FloatScale));
        }

        private void AddByte(byte value)
        {
            _value = (_value ^ value) * Prime;
        }
    }
}
