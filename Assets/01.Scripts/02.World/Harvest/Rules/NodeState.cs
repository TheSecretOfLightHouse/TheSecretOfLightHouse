using System;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public struct NodeState : IEquatable<NodeState>
    {
        public ushort ResourceId;
        public ushort Remaining;

        public NodeState(int resourceId, int remaining)
        {
            ResourceId = (ushort)Mathf.Clamp(resourceId, 0, ushort.MaxValue);
            Remaining = (ushort)Mathf.Clamp(remaining, 0, ushort.MaxValue);
        }

        public bool Equals(NodeState other)
        {
            return ResourceId == other.ResourceId && Remaining == other.Remaining;
        }

        public override bool Equals(object obj)
        {
            return obj is NodeState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ResourceId, Remaining);
        }
    }
}
