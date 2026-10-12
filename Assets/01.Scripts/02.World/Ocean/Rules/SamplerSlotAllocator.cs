using System;

namespace Lighthouse.World.Ocean.Rules
{
    public class SamplerSlotAllocator
    {
        private readonly bool[] _isUsed;
        private readonly int _shipCapacity;
        private int _activeLength;

        public SamplerSlotAllocator(int shipCapacity, int driftCapacity)
        {
            _shipCapacity = Math.Max(0, shipCapacity);
            _isUsed = new bool[_shipCapacity + Math.Max(0, driftCapacity)];
        }

        public int Capacity => _isUsed.Length;

        public int ActiveLength => _activeLength;

        public bool TryAllocate(SamplerPool pool, int count, out SamplerTicket ticket)
        {
            ticket = default;
            if (count <= 0)
            {
                return false;
            }

            int begin = pool == SamplerPool.Ship ? 0 : _shipCapacity;
            int end = pool == SamplerPool.Ship ? _shipCapacity : _isUsed.Length;
            int runLength = 0;

            for (int i = begin; i < end; i++)
            {
                runLength = _isUsed[i] ? 0 : runLength + 1;
                if (runLength < count)
                {
                    continue;
                }

                int start = i - count + 1;
                for (int slot = start; slot <= i; slot++)
                {
                    _isUsed[slot] = true;
                }

                _activeLength = Math.Max(_activeLength, i + 1);
                ticket = new SamplerTicket(start, count);
                return true;
            }

            return false;
        }

        public void Release(in SamplerTicket ticket)
        {
            for (int i = 0; i < ticket.Count; i++)
            {
                int slot = ticket.Start + i;
                if (slot >= 0 && slot < _isUsed.Length)
                {
                    _isUsed[slot] = false;
                }
            }

            _activeLength = FindActiveLength();
        }

        private int FindActiveLength()
        {
            for (int i = _isUsed.Length - 1; i >= 0; i--)
            {
                if (_isUsed[i])
                {
                    return i + 1;
                }
            }

            return 0;
        }
    }
}
