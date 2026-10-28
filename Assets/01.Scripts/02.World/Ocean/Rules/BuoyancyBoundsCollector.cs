using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BuoyancyBoundsCollector
    {
        private const int CornerCount = 8;
        private const float DiameterFactor = 2f;

        public static bool TryCollect(
            Rigidbody body,
            List<Collider> scratch,
            out Bounds bounds)
        {
            bounds = default;

            if (body == null || scratch == null)
            {
                return false;
            }

            body.GetComponentsInChildren(false, scratch);

            Transform root = body.transform;
            bool hasBounds = false;

            for (int i = 0; i < scratch.Count; i++)
            {
                Collider collider = scratch[i];

                if (collider == null
                    || !collider.enabled
                    || collider.isTrigger
                    || collider.attachedRigidbody != body
                    || !TryGetLocalBounds(collider, out Bounds local))
                {
                    continue;
                }

                Transform source = collider.transform;

                for (int corner = 0; corner < CornerCount; corner++)
                {
                    Vector3 point = root.InverseTransformPoint(
                        source.TransformPoint(GetCorner(local, corner)));

                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            scratch.Clear();

            return hasBounds;
        }

        private static Vector3 GetCorner(Bounds local, int index)
        {
            Vector3 extents = local.extents;

            return new Vector3(
                local.center.x + ((index & 1) == 0 ? -extents.x : extents.x),
                local.center.y + ((index & 2) == 0 ? -extents.y : extents.y),
                local.center.z + ((index & 4) == 0 ? -extents.z : extents.z));
        }

        private static bool TryGetLocalBounds(Collider collider, out Bounds local)
        {
            switch (collider)
            {
                case BoxCollider box:
                    local = new Bounds(box.center, box.size);
                    return true;

                case SphereCollider sphere:
                    local = new Bounds(
                        sphere.center,
                        Vector3.one * (sphere.radius * DiameterFactor));
                    return true;

                case CapsuleCollider capsule:
                    float diameter = capsule.radius * DiameterFactor;
                    float length = Mathf.Max(capsule.height, diameter);

                    Vector3 size = capsule.direction == 0
                        ? new Vector3(length, diameter, diameter)
                        : capsule.direction == 1
                            ? new Vector3(diameter, length, diameter)
                            : new Vector3(diameter, diameter, length);

                    local = new Bounds(capsule.center, size);
                    return true;

                case MeshCollider mesh when mesh.sharedMesh != null:
                    local = mesh.sharedMesh.bounds;
                    return true;
            }

            local = default;

            return false;
        }
    }
}
