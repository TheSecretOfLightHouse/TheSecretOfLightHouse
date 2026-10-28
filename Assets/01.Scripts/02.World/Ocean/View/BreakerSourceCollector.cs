using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    public sealed class BreakerSourceCollector
    {
        public const int Capacity = 32;
        private const float ScanRadius = 48f;
        private const float ScanHalfHeight = 3f;
        private const float RefreshInterval = 0.5f;
        private readonly Collider[] _colliders = new Collider[1024];
        private readonly BreakerSource[] _sources = new BreakerSource[Capacity];
        private readonly float[] _distances = new float[Capacity];
        private readonly BreakerWaveTuning _tuning;
        private float _nextScan;

        public int Count { get; private set; }
        public int CandidateCount { get; private set; }
        public int SaturatedScanCount { get; private set; }
        public ReadOnlySpan<BreakerSource> Sources => new ReadOnlySpan<BreakerSource>(_sources, 0, Count);

        public BreakerSourceCollector(in BreakerWaveTuning tuning)
        {
            _tuning = tuning;
        }

        public void Refresh(Vector3 focus, float seaLevel, Vector2 swell, float now)
        {
            if (now < _nextScan)
            {
                return;
            }

            _nextScan = now + RefreshInterval;
            Count = 0;
            CandidateCount = 0;
            int found = Physics.OverlapBoxNonAlloc(new Vector3(focus.x, seaLevel, focus.z),
                new Vector3(ScanRadius, ScanHalfHeight, ScanRadius), _colliders, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            if (found == _colliders.Length)
            {
                SaturatedScanCount++;
            }

            Vector2 focusXz = new Vector2(focus.x, focus.z);
            for (int i = 0; i < found; i++)
            {
                Collider collider = _colliders[i];
                if (collider.isTrigger || collider.attachedRigidbody != null)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                Vector2 approach = BreakerApproach.Resolve(CoastWaveRegistry.Bounds, CoastWaveRegistry.Edges,
                    new Vector2(bounds.center.x, bounds.center.z), swell, out float exposure);
                if (!BreakerSourceFilter.TryCreate(in _tuning, collider.GetInstanceID(), bounds.center, bounds.extents,
                    seaLevel, approach, exposure, out BreakerSource source)
                    || BreakerSourceFilter.HasNeighbor(in _tuning, Sources, source.Center))
                {
                    continue;
                }

                CandidateCount++;
                Insert(source, (source.Center - focusXz).sqrMagnitude);
            }
        }

        private void Insert(in BreakerSource source, float distance)
        {
            int index = Count;
            if (Count == Capacity)
            {
                if (distance >= _distances[Capacity - 1])
                {
                    return;
                }

                index = Capacity - 1;
            }
            else
            {
                Count++;
            }

            while (index > 0 && _distances[index - 1] > distance)
            {
                _sources[index] = _sources[index - 1];
                _distances[index] = _distances[index - 1];
                index--;
            }

            _sources[index] = source;
            _distances[index] = distance;
        }
    }
}
