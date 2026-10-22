using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public readonly struct ShoreDropResult
    {
        public readonly Vector3 Position;
        public readonly short IslandIndex;
        public readonly ShoreFailure Failure;

        private ShoreDropResult(Vector3 position, short islandIndex, ShoreFailure failure)
        {
            Position = position;
            IslandIndex = islandIndex;
            Failure = failure;
        }

        public bool IsSuccess => Failure == ShoreFailure.None;

        public static ShoreDropResult Success(Vector3 position, short islandIndex)
        {
            return new ShoreDropResult(position, islandIndex, ShoreFailure.None);
        }

        public static ShoreDropResult Fail(ShoreFailure failure)
        {
            return new ShoreDropResult(Vector3.zero, -1, failure);
        }
    }
}
