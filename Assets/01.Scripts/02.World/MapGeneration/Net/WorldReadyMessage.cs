using Mirror;

namespace Lighthouse.World.MapGeneration.Net
{
    public struct WorldReadyMessage : NetworkMessage
    {
        public uint LayoutHash;
        public uint ContentHash;
    }
}
