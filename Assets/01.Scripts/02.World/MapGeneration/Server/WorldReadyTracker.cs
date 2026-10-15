using System;
using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.MapGeneration.Server
{
    public sealed class WorldReadyTracker : IWorldReadyGate
    {
        private readonly HashSet<uint> _ready = new HashSet<uint>();

        public event Action Changed;

        public int ReadyCount => _ready.Count;

        public bool AreAllReady(IReadOnlyList<uint> roster)
        {
            if (roster == null || roster.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                if (!_ready.Contains(roster[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public MapChecksumResult Submit(uint actorId, MapSeedInfo expected, uint layoutHash, uint contentHash)
        {
            MapChecksumResult result = expected.Verify(layoutHash, contentHash);

            if (result == MapChecksumResult.Match && _ready.Add(actorId))
            {
                Changed?.Invoke();
            }

            return result;
        }

        public void Clear()
        {
            _ready.Clear();
        }
    }
}
