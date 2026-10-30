using System;
using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    public sealed class WalkBoundaryBaker
    {
        private readonly WalkBoundaryConfig _config;
        private readonly WalkBoundary _boundary;
        private readonly Queue<WalkMaskBuilder> _pending = new Queue<WalkMaskBuilder>();

        public WalkBoundaryBaker(WalkBoundaryConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _boundary = new WalkBoundary(config.LookaheadMeters);
        }

        public IWalkBoundary Boundary => _boundary;
        public bool IsIdle => _pending.Count == 0;

        public void Enqueue(IslandSpawnPlan plan, IGroundHeightSource ground)
        {
            var center = new Vector2(plan.Position.x, plan.Position.z);
            _pending.Enqueue(new WalkMaskBuilder(
                ground,
                plan.Index,
                center,
                plan.Archetype.Radius,
                _config.CellSize,
                _config.SeaLevel - _config.WadeDepth,
                _config.MarginMeters));
        }

        public void Tick()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            WalkMaskBuilder builder = _pending.Peek();
            builder.SampleRows(_config.RowsPerTick);
            if (builder.IsSampled)
            {
                _boundary.Add(_pending.Dequeue().Build());
            }
        }

        public void Clear()
        {
            _pending.Clear();
            _boundary.Clear();
        }
    }
}
