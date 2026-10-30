using System.Diagnostics;
using System.Text;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public static class WalkBoundaryBakeProbe
    {
        private const short ProbeIndex = 1;
        private const float PlacementOffset = 2000f;
        private const int MapStep = 2;

        public static string Bake(GameObject prefab, float yawDegrees, float radius, WalkBoundaryConfig config)
        {
            var position = new Vector3(PlacementOffset, 0f, PlacementOffset);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yawDegrees, 0f));
            try
            {
                Physics.SyncTransforms();
                var roots = new SingleRoot(instance.transform);
                var ground = new IslandGroundHeightSource(roots);
                var center = new Vector2(position.x, position.z);
                var builder = new WalkMaskBuilder(
                    ground, ProbeIndex, center, radius, config.CellSize, config.SeaLevel - config.WadeDepth, config.MarginMeters);

                var watch = Stopwatch.StartNew();
                WalkMask mask = builder.Build();
                watch.Stop();

                return Describe(mask, center, radius, builder.RowCount, watch.ElapsedMilliseconds);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static string Describe(WalkMask mask, Vector2 center, float radius, int rows, long milliseconds)
        {
            var text = new StringBuilder();
            text.Append($"ms={milliseconds} rows={rows}\n");
            int reach = Mathf.CeilToInt(radius) + 1;
            for (int z = reach; z >= -reach; z -= MapStep)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    text.Append(mask.IsWalkable(center + new Vector2(x, z)) ? '#' : '.');
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        public sealed class SingleRoot : IIslandRootSource
        {
            private readonly Transform _root;

            public SingleRoot(Transform root)
            {
                _root = root;
            }

            public bool TryGetRoot(short islandIndex, out Transform root)
            {
                root = _root;
                return _root != null;
            }
        }
    }
}
