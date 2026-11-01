using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Rules
{
    public readonly struct IllusionRecord
    {
        public readonly int Id;
        public readonly Vector3 Position;
        public readonly int PlacedDay;
        public readonly uint CasterId;

        public IllusionRecord(int id, Vector3 position, int placedDay, uint casterId)
        {
            Id = id;
            Position = position;
            PlacedDay = placedDay;
            CasterId = casterId;
        }
    }
}
