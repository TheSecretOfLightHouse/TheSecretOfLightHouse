using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    public sealed class IslandGroundHeightSource : IGroundHeightSource
    {
        private const int HitBufferSize = 32;
        private const float CastHeight = 100f;
        private const float CastDistance = 250f;

        private readonly IIslandRootSource _roots;
        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];

        public IslandGroundHeightSource(IIslandRootSource roots)
        {
            _roots = roots;
        }

        public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
        {
            height = 0f;
            if (_roots == null || !_roots.TryGetRoot(islandIndex, out Transform root) || root == null)
            {
                return false;
            }

            var origin = new Vector3(x, CastHeight, z);
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _hits,
                CastDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            bool isFound = false;
            float best = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _hits[i].collider;
                if (hitCollider == null || !hitCollider.transform.IsChildOf(root))
                {
                    continue;
                }

                float y = _hits[i].point.y;
                if (y > best)
                {
                    best = y;
                    isFound = true;
                }
            }

            height = best;
            return isFound;
        }
    }
}
