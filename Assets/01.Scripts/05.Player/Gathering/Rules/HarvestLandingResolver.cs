using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Player.Gathering.Rules
{
    public sealed class HarvestLandingResolver
    {
        private const int Capacity = 64;
        private const int DirectionCount = 12;
        private const float GroundClearance = 0.03f;
        private const float CastHeight = 8f;
        private const float CastDistance = 16f;
        private const float MaxLandingRise = 2f;
        private readonly RaycastHit[] _hits = new RaycastHit[Capacity];
        private readonly Collider[] _overlaps = new Collider[Capacity];

        public bool TryResolve(Vector3 source, Vector3 actor, GameObject prefab,
            out Vector3 start, out Vector3 landing)
        {
            start = source + Vector3.up * 0.6f;
            landing = default;
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            Bounds visual = new Bounds();
            bool hasVisual = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                {
                    continue;
                }

                if (!hasVisual)
                {
                    visual = renderers[i].bounds;
                    hasVisual = true;
                }
                else
                {
                    visual.Encapsulate(renderers[i].bounds);
                }
            }

            float bottomOffset = hasVisual ? visual.min.y - prefab.transform.position.y : 0f;
            float radius = hasVisual ? Mathf.Max(visual.extents.x, visual.extents.z, 0.12f) : 0.2f;
            float height = hasVisual ? Mathf.Max(visual.size.y, 0.2f) : 0.4f;
            float angle = Mathf.Atan2(actor.z - source.z, actor.x - source.x);

            for (int ring = 0; ring < 3; ring++)
            {
                float distance = 0.9f + radius + ring * 0.7f;
                for (int i = 0; i < DirectionCount; i++)
                {
                    float offset = ((i + 1) / 2) * (i % 2 == 0 ? 1f : -1f);
                    float theta = angle + offset * Mathf.PI * 2f / DirectionCount;
                    Vector3 candidate = source + new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * distance;
                    if (!TryGround(candidate, out RaycastHit ground))
                    {
                        continue;
                    }

                    if ((WorldServices.TryGet<IWalkBoundary>(out var boundary) && !boundary.IsWalkable(ground.point))
                        || (WorldServices.TryGet<IWorldQuery>(out var world)
                            && ground.point.y < world.SampleWaterHeight(ground.point) + GroundClearance))
                    {
                        continue;
                    }

                    if (IsOccupied(ground.point, radius))
                    {
                        continue;
                    }

                    Vector3 foot = ground.point + Vector3.up * GroundClearance;
                    Vector3 center = foot + Vector3.up * (height * 0.5f);
                    int count = Physics.OverlapBoxNonAlloc(center,
                        new Vector3(radius, height * 0.5f, radius), _overlaps,
                        Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (count == Capacity)
                    {
                        continue;
                    }

                    bool isBlocked = false;
                    for (int j = 0; j < count; j++)
                    {
                        if (_overlaps[j] != ground.collider)
                        {
                            isBlocked = true;
                            break;
                        }
                    }

                    if (isBlocked)
                    {
                        continue;
                    }

                    landing = foot - Vector3.up * bottomOffset;
                    start.y = Mathf.Max(start.y, ground.point.y + 0.6f - bottomOffset);
                    return true;
                }
            }

            return false;
        }

        private static bool IsOccupied(Vector3 position, float radius)
        {
            var pickups = ResourcePickup.Active;
            for (int i = 0; i < pickups.Count; i++)
            {
                ResourcePickup pickup = pickups[i];
                if (!pickup)
                {
                    continue;
                }

                Vector3 delta = pickup.RestingPosition - position;
                float separation = radius + pickup.FootprintRadius + GroundClearance;
                if (Mathf.Abs(delta.y) < 1f && delta.x * delta.x + delta.z * delta.z < separation * separation)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGround(Vector3 candidate, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(candidate + Vector3.up * CastHeight,
                Vector3.down, _hits, CastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == Capacity)
            {
                return false;
            }

            bool isFound = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.normal.y < 0.75f || hit.point.y > candidate.y + MaxLandingRise
                    || hit.collider.GetComponentInParent<HarvestSpot>()
                    || hit.collider.GetComponentInParent<ResourcePickup>()
                    || hit.collider.GetComponentInParent<PlayerController>()
                    || hit.collider.GetComponentInParent<BoatController>())
                {
                    continue;
                }

                // Prefer the actual terrain over decorative props above it.
                bool isTerrain = hit.collider is TerrainCollider;
                bool hadTerrain = isFound && ground.collider is TerrainCollider;
                if (!isFound || (isTerrain && !hadTerrain)
                    || (isTerrain == hadTerrain && hit.point.y > ground.point.y))
                {
                    ground = hit;
                    isFound = true;
                }
            }

            return isFound;
        }
    }
}
