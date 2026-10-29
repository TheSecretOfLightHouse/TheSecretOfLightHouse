using System;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public static class WalkBoundaryChecks
    {
        private const float CellSize = 0.5f;
        private const float DiscRadius = 10f;
        private const float MinHeight = 1f;
        private const float Lookahead = 0.6f;
        private const float Tolerance = 0.001f;

        public static bool RunAll()
        {
            bool isPassed = RunMask();
            isPassed &= RunBuilder();
            isPassed &= RunFilter();
            return isPassed;
        }

        private static bool RunMask()
        {
            WalkMask mask = BuildDisc(Vector2.zero, 0f);
            WalkMask margin = BuildDisc(Vector2.zero, 1f);

            bool isPassed =
                mask.IsWalkable(Vector2.zero)
                && mask.IsWalkable(new Vector2(9f, 0f))
                && !mask.IsWalkable(new Vector2(11f, 0f))
                && !mask.IsWalkable(new Vector2(500f, 0f))
                && mask.Contains(new Vector2(10.8f, 0f))
                && !mask.Contains(new Vector2(500f, 0f))
                && !margin.IsWalkable(new Vector2(12f, 0f))
                && margin.IsWalkable(new Vector2(10.8f, 0f))
                && !mask.IsWalkable(new Vector2(10.8f, 0f));

            Report("Mask", isPassed, "inside/outside/bounds/margin");
            return isPassed;
        }

        private static bool RunBuilder()
        {
            var ground = new DiscGround(Vector2.zero, DiscRadius);
            var stepped = new WalkMaskBuilder(ground, 0, Vector2.zero, DiscRadius, CellSize, MinHeight, 0f);
            while (!stepped.IsSampled)
            {
                stepped.SampleRows(7);
            }

            WalkMask partial = stepped.Build();
            WalkMask whole = BuildDisc(Vector2.zero, 0f);

            bool isSame = true;
            for (float x = -13f; x <= 13f && isSame; x += 0.37f)
            {
                for (float z = -13f; z <= 13f; z += 0.41f)
                {
                    var point = new Vector2(x, z);
                    if (partial.IsWalkable(point) != whole.IsWalkable(point))
                    {
                        isSame = false;
                        break;
                    }
                }
            }

            bool isPassed =
                isSame
                && Throws<ArgumentNullException>(() => new WalkMaskBuilder(null, 0, Vector2.zero, DiscRadius, CellSize, MinHeight, 0f))
                && Throws<ArgumentOutOfRangeException>(() => new WalkMaskBuilder(ground, 0, Vector2.zero, DiscRadius, 0f, MinHeight, 0f))
                && Throws<ArgumentException>(() => new WalkMask(Vector2.zero, CellSize, 2, 2, new bool[3]));

            Report("Builder", isPassed, "incremental/null/invalid");
            return isPassed;
        }

        private static bool RunFilter()
        {
            var boundary = new WalkBoundary(Lookahead);
            boundary.Add(BuildDisc(Vector2.zero, 0f));

            Vector3 edge = new Vector3(9.6f, 0.7f, 0f);
            Vector3 blocked = boundary.FilterMove(edge, new Vector3(1f, 0f, 0f));
            Vector3 slid = boundary.FilterMove(edge, new Vector3(1f, 0f, 1f));
            Vector3 inward = boundary.FilterMove(edge, new Vector3(-1f, 0f, 0f));
            Vector3 interior = boundary.FilterMove(Vector3.zero, new Vector3(1f, 0f, 0f));
            Vector3 still = boundary.FilterMove(edge, Vector3.zero);
            Vector3 open = boundary.FilterMove(new Vector3(50f, 0f, 0f), new Vector3(1f, 0f, 0f));

            bool isPassed =
                NearVector(blocked, Vector3.zero)
                && NearVector(slid, new Vector3(0f, 0f, 1f))
                && NearVector(inward, new Vector3(-1f, 0f, 0f))
                && NearVector(interior, new Vector3(1f, 0f, 0f))
                && NearVector(still, Vector3.zero)
                && NearVector(open, new Vector3(1f, 0f, 0f))
                && boundary.IsWalkable(Vector3.zero)
                && !boundary.IsWalkable(new Vector3(50f, 0f, 0f))
                && Throws<ArgumentOutOfRangeException>(() => new WalkBoundary(0f))
                && Throws<ArgumentNullException>(() => boundary.Add(null));

            Vector3 outside = boundary.FilterMove(new Vector3(10.2f, 0.7f, 0f), new Vector3(1f, 0f, 0.2f));
            isPassed &= outside.x < 0f && Mathf.Abs(outside.z) < Tolerance
                && NearVector(new Vector3(outside.x, 0f, outside.z), new Vector3(-Mathf.Sqrt(1.04f), 0f, 0f));

            boundary.Add(BuildDisc(new Vector2(15f, 0f), 0f));
            Vector3 overlap = boundary.FilterMove(edge, new Vector3(1f, 0f, 0f));
            isPassed &= NearVector(overlap, new Vector3(1f, 0f, 0f)) && boundary.MaskCount == 2;

            boundary.Clear();
            isPassed &= boundary.MaskCount == 0 && NearVector(boundary.FilterMove(edge, new Vector3(1f, 0f, 0f)), new Vector3(1f, 0f, 0f));

            Report("Filter", isPassed, "block/slide/inward/interior/still/open/overlap/clear");
            return isPassed;
        }

        private static WalkMask BuildDisc(Vector2 center, float margin)
        {
            var ground = new DiscGround(center, DiscRadius);
            return new WalkMaskBuilder(ground, 0, center, DiscRadius, CellSize, MinHeight, margin).Build();
        }

        private static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T)
            {
                return true;
            }
        }

        private static bool NearVector(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= Tolerance * Tolerance;
        }

        private static void Report(string name, bool isPassed, string detail)
        {
            Debug.Log($"[{nameof(WalkBoundaryChecks)}] {name} {(isPassed ? "PASS" : "FAIL")} {detail}");
        }

        private sealed class DiscGround : IGroundHeightSource
        {
            private const float GroundHeight = 2f;

            private readonly Vector2 _center;
            private readonly float _radius;

            public DiscGround(Vector2 center, float radius)
            {
                _center = center;
                _radius = radius;
            }

            public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
            {
                bool isInside = (new Vector2(x, z) - _center).sqrMagnitude <= _radius * _radius;
                height = isInside ? GroundHeight : 0f;
                return isInside;
            }
        }
    }
}
