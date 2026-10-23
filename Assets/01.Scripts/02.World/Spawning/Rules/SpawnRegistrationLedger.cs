using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    public sealed class SpawnRegistrationLedger
    {
        private readonly Dictionary<SpawnerKey, long> _batches = new Dictionary<SpawnerKey, long>();

        public int Count => _batches.Count;

        public bool Contains(SpawnerPoint point)
        {
            return _batches.ContainsKey(new SpawnerKey(point));
        }

        public bool TryAdd(SpawnerPoint point, long batchId)
        {
            return _batches.TryAdd(new SpawnerKey(point), batchId);
        }

        public void CopyBatchIds(List<long> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            foreach (long batchId in _batches.Values)
            {
                results.Add(batchId);
            }
        }

        public void Clear()
        {
            _batches.Clear();
        }

        private readonly struct SpawnerKey : IEquatable<SpawnerKey>
        {
            private readonly Vector3 _position;
            private readonly int _profileId;

            public SpawnerKey(SpawnerPoint point)
            {
                _position = point.Position;
                _profileId = point.ProfileId;
            }

            public bool Equals(SpawnerKey other)
            {
                return _position == other._position && _profileId == other._profileId;
            }

            public override bool Equals(object obj)
            {
                return obj is SpawnerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_position.x, _position.y, _position.z, _profileId);
            }
        }
    }
}
