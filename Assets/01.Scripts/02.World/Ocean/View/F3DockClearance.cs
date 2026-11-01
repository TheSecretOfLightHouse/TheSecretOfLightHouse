using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    // The baked ground mask treats a dock deck as land, so the sea under and past it is clipped and the backdrop shows
    // through as a flat hole. Water under a deck is real water; clear the mask there. Sand under the deck's landward end
    // still hides the water through the depth test.
    public static class F3DockClearance
    {
        private const string DockPrefix = "Dock_";
        private const float Margin = 0.15f;
        private const float MinFootprint = 0.8f;

        private static readonly List<Collider> Colliders = new List<Collider>(32);
        private static readonly List<Renderer> Renderers = new List<Renderer>(32);

        public static int Clear(Color32[] coverage, int size, int startX, int startZ, int tileSize,
            Vector2 origin, float cell, Transform island)
        {
            if (coverage == null || island == null || cell <= 0f)
            {
                return 0;
            }

            int cleared = 0;
            island.GetComponentsInChildren(true, Colliders);
            for (int i = 0; i < Colliders.Count; i++)
            {
                Collider collider = Colliders[i];
                if (collider != null && IsDeck(collider.name, collider.bounds))
                {
                    cleared += ClearBounds(coverage, size, startX, startZ, tileSize, origin, cell, collider.bounds);
                }
            }
            island.GetComponentsInChildren(true, Renderers);
            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderer renderer = Renderers[i];
                if (renderer != null && IsDeck(renderer.name, renderer.bounds))
                {
                    cleared += ClearBounds(coverage, size, startX, startZ, tileSize, origin, cell, renderer.bounds);
                }
            }
            Colliders.Clear();
            Renderers.Clear();
            return cleared;
        }

        private static bool IsDeck(string objectName, Bounds bounds)
        {
            return objectName.StartsWith(DockPrefix) && bounds.size.x >= MinFootprint && bounds.size.z >= MinFootprint
                && !objectName.Contains("Lantern") && !objectName.Contains("Crate");
        }

        private static int ClearBounds(Color32[] coverage, int size, int startX, int startZ, int tileSize,
            Vector2 origin, float cell, Bounds bounds)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((bounds.min.x - Margin - origin.x) / cell));
            int maxX = Mathf.Min(tileSize - 1, Mathf.FloorToInt((bounds.max.x + Margin - origin.x) / cell));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((bounds.min.z - Margin - origin.y) / cell));
            int maxZ = Mathf.Min(tileSize - 1, Mathf.FloorToInt((bounds.max.z + Margin - origin.y) / cell));
            int cleared = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int target = (startZ + z) * size + startX + x;
                    if (coverage[target].r != 0)
                    {
                        coverage[target] = default;
                        cleared++;
                    }
                }
            }
            return cleared;
        }
    }
}
