using Lighthouse.UI.SharedMap.Rules;
using Mirror;

namespace Lighthouse.UI.SharedMap.Net
{
    public struct SharedMapRequest : NetworkMessage
    {
        // 0 requests a bounded replay chunk, 1 readies the sender, 2 appends an operation.
        public byte Action;
        public uint Epoch;
        public int Offset;
        public SharedMapOperation Operation;
        public SharedMapOperation[] Operations;
    }
}
