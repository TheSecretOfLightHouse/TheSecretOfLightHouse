using System;
using System.Collections.Generic;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class WaveModifierRegistry
    {
        private readonly List<Entry> _sources = new List<Entry>();
        private int _reservedCount;

        public int SourceCount => _sources.Count;
        public int ReservedCount => _reservedCount;

        public bool Register(IWaveModifierSource source)
        {
            if (source == null)
            {
                return false;
            }

            int count = source.MaxCount;
            if (count <= 0 || count > WaveModifierModel.MaxModifiers - _reservedCount)
            {
                return false;
            }

            for (int i = 0; i < _sources.Count; i++)
            {
                if (ReferenceEquals(_sources[i].Source, source))
                {
                    return false;
                }
            }

            _sources.Add(new Entry(source, count));
            _reservedCount += count;
            return true;
        }

        public bool Unregister(IWaveModifierSource source)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                if (ReferenceEquals(_sources[i].Source, source))
                {
                    _reservedCount -= _sources[i].Capacity;
                    _sources.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool TryGather(Span<WaveModifierData> destination, out int count)
        {
            count = 0;
            if (destination.Length < _reservedCount)
            {
                return false;
            }

            for (int i = 0; i < _sources.Count; i++)
            {
                Entry entry = _sources[i];
                int written = entry.Source.WriteModifiers(
                    destination.Slice(count, entry.Capacity));

                if (written < 0 || written > entry.Capacity)
                {
                    count = 0;
                    return false;
                }

                count += written;
            }

            if (!WaveModifierModel.AreValid(destination.Slice(0, count)))
            {
                count = 0;
                return false;
            }

            return true;
        }

        private readonly struct Entry
        {
            public readonly IWaveModifierSource Source;
            public readonly int Capacity;

            public Entry(IWaveModifierSource source, int capacity)
            {
                Source = source;
                Capacity = capacity;
            }
        }
    }
}
