using Lighthouse.UI.SharedMap.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Net
{
    public struct SharedMapUpdate : NetworkMessage
    {
        public uint Epoch;
        public int Offset;
        public int Total;
        public SharedMapOperation[] Operations;
        public Vector2 Lighthouse;
        public Vector3 TablePosition;
        public float Range;
        public byte ReadyCount;
        public bool IsNight;
        public bool IsEditing;
        public double Deadline;
        public uint EditSession;
    }
}
