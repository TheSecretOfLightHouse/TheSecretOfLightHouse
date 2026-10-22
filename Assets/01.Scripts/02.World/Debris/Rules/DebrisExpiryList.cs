using System.Collections.Generic;

namespace Lighthouse.World.Debris.Rules
{
    public sealed class DebrisExpiryList
    {
        private readonly List<DebrisExpiryEntry> _entries = new List<DebrisExpiryEntry>();

        public int Count => _entries.Count;

        public void Add(IExpirableDrop drop, ulong generation, double expireAt)
        {
            _entries.Add(new DebrisExpiryEntry(drop, generation, expireAt));
        }

        public bool Remove(IExpirableDrop drop)
        {
            bool isRemoved = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_entries[i].Drop, drop))
                {
                    RemoveAtSwap(i);
                    isRemoved = true;
                }
            }

            return isRemoved;
        }

        public int CollectExpired(double now, List<DebrisExpiryEntry> results)
        {
            int collected = 0;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].ExpireAt <= now)
                {
                    results.Add(_entries[i]);
                    RemoveAtSwap(i);
                    collected++;
                }
            }

            return collected;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        private void RemoveAtSwap(int index)
        {
            int last = _entries.Count - 1;
            _entries[index] = _entries[last];
            _entries.RemoveAt(last);
        }
    }
}
