using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    public static class CoastalShelfMesh
    {
        private const int Segments = 96;
        private const float TwoPi = Mathf.PI * 2f;
        private static readonly float[] RingOffsets =
        {
            -4f, 0f, 0.2f, 0.45f, 0.7f, 0.9f, 1f,
            1.12f, 1.3f, 1.5f, 1.8f, 2.2f, 2.8f, 3.6f
        };
        private static readonly float[] RingDepths =
        {
            0.12f, 0.22f, 0.28f, 0.34f, 0.4f, 0.48f, 0.55f,
            0.7f, 1.4f, 2.8f, 5f, 9f, 16f, 32f
        };

        public static Mesh Build(MeshFilter ground, Transform island, float width, Vector4 profile)
        {
            Mesh source = ground.sharedMesh;
            if (source == null || !source.isReadable || width <= 0f)
            {
                return null;
            }

            Matrix4x4 toIsland = island.worldToLocalMatrix * ground.transform.localToWorldMatrix;
            Vector3[] points = source.vertices;
            int[] triangles = source.triangles;
            Vector3 center = toIsland.MultiplyPoint3x4(source.bounds.center);
            float waterY = island.InverseTransformPoint(
                new Vector3(island.position.x, 0f, island.position.z)).y;
            center.y = waterY;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = toIsland.MultiplyPoint3x4(points[i]);
            }

            var radii = new float[Segments];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = points[triangles[i]];
                Vector3 b = points[triangles[i + 1]];
                Vector3 c = points[triangles[i + 2]];
                AddCrossing(a, b, center, radii);
                AddCrossing(b, c, center, radii);
                AddCrossing(c, a, center, radii);
            }

            for (int i = 0; i < Segments; i++)
            {
                if (radii[i] <= 0f)
                {
                    return null;
                }
            }

            int side = Segments + 1;
            var vertices = new Vector3[side * RingOffsets.Length];
            var uv = new Vector2[vertices.Length];
            for (int ring = 0; ring < RingOffsets.Length; ring++)
            {
                for (int i = 0; i <= Segments; i++)
                {
                    int sector = i % Segments;
                    float angle = sector * TwoPi / Segments;
                    float radius = (radii[(sector + Segments - 1) % Segments]
                        + 2f * radii[sector] + radii[(sector + 1) % Segments]) * 0.25f;
                    Vector3 shore = island.TransformPoint(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                    float localWidth = Lighthouse.World.Ocean.Rules.CoastWaveModel.WidthAt(width, profile, shore.x, shore.z);
                    float rock = 1f - Mathf.InverseLerp(width * 0.14f, width, localWidth);
                    float offset = ring == 0 ? RingOffsets[ring] : RingOffsets[ring] * localWidth;
                    float variation = ring == 0 ? 0f : Mathf.Sin(angle * 3f + 0.7f) * width * 0.04f;
                    radius = Mathf.Max(0.1f, radius + offset + variation);
                    int index = ring * side + i;
                    float depth = Mathf.Lerp(RingDepths[ring], ring < 2 ? RingDepths[ring]
                        : Mathf.Min(32f, 0.5f + (ring - 1) * 2f), rock);
                    vertices[index] = center + new Vector3(
                        Mathf.Cos(angle) * radius, -depth, Mathf.Sin(angle) * radius);
                    uv[index] = new Vector2(vertices[index].x, vertices[index].z) * 0.1f;
                }
            }

            var indices = new int[(RingOffsets.Length - 1) * Segments * 6];
            int cursor = 0;
            for (int ring = 0; ring < RingOffsets.Length - 1; ring++)
            {
                for (int i = 0; i < Segments; i++)
                {
                    int a = ring * side + i;
                    int b = a + side;
                    indices[cursor++] = a;
                    indices[cursor++] = a + 1;
                    indices[cursor++] = b;
                    indices[cursor++] = a + 1;
                    indices[cursor++] = b + 1;
                    indices[cursor++] = b;
                }
            }

            var mesh = new Mesh { name = "CoastalShelf_Runtime" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCrossing(Vector3 a, Vector3 b, Vector3 center, float[] radii)
        {
            float low = a.y - center.y;
            float high = b.y - center.y;
            if ((low < 0f) == (high < 0f) || Mathf.Abs(high - low) < 0.0001f)
            {
                return;
            }

            Vector3 crossing = Vector3.LerpUnclamped(a, b, -low / (high - low)) - center;
            float angle = Mathf.Repeat(Mathf.Atan2(crossing.z, crossing.x), TwoPi);
            int sector = Mathf.FloorToInt(angle * Segments / TwoPi) % Segments;
            float radius = new Vector2(crossing.x, crossing.z).magnitude;
            for (int i = -1; i <= 1; i++)
            {
                int index = (sector + i + Segments) % Segments;
                radii[index] = Mathf.Max(radii[index], radius);
            }
        }

        public static Mesh BuildRockEdge(Mesh shelf, Transform island, float width, Vector4 profile, out Mesh[] collisionMeshes)
        {
            Vector3[] shore = shelf.vertices;
            var vertices = new System.Collections.Generic.List<Vector3>(Segments * 8);
            var indices = new System.Collections.Generic.List<int>(Segments * 18);
            var colliders = new System.Collections.Generic.List<Mesh>(Segments / 2);
            for (int sector = 0; sector < Segments; sector++)
            {
                Vector3 a = shore[Segments + 1 + sector];
                Vector3 b = shore[Segments + 2 + sector];
                Vector3 world = island.TransformPoint((a + b) * 0.5f);
                float localWidth = Lighthouse.World.Ocean.Rules.CoastWaveModel.WidthAt(width, profile, world.x, world.z);
                float rock = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 0.25f, width * 0.55f, localWidth));
                if (rock <= 0f)
                {
                    continue;
                }
                if (sector % 2 != 0)
                {
                    continue;
                }
                int start = vertices.Count;
                int firstIndex = indices.Count;
                Vector3 midpoint = (a + b) * 0.5f;
                Vector3 outward = (midpoint - island.InverseTransformPoint(new Vector3(profile.x, 0f, profile.y)));
                outward.y = 0f;
                outward.Normalize();
                float seed = sector * 17.13f;
                float jitter = Mathf.Sin(seed) * 0.5f;
                Vector3 tangent = Vector3.Cross(Vector3.up, outward);
                midpoint += outward * (0.12f + jitter * 0.9f) + tangent * (0.55f * Mathf.Sin(seed * 1.3f));
                float height = (0.3f + 0.18f * Mathf.Sin(seed * 1.7f) + 0.15f) * rock;
                float radius = 1.2f + 0.45f * Mathf.Sin(seed * 2.3f);
                for (int ring = 0; ring < 3; ring++)
                {
                    for (int corner = 0; corner < 7; corner++)
                    {
                        float angle = corner * TwoPi / 7f + jitter;
                        float scale = ring == 0 ? 0.8f : ring == 1 ? 1f : 0.5f;
                        float r = radius * scale * (1f + 0.12f * Mathf.Sin(corner * 4.3f + seed));
                        float y = ring == 0 ? -1.8f : ring == 1 ? height * 0.25f : height;
                        vertices.Add(new Vector3(midpoint.x + Mathf.Cos(angle) * r,
                            y + (ring > 0 ? 0.08f * Mathf.Sin(seed + corner * 2.4f) : 0f),
                            midpoint.z + Mathf.Sin(angle) * r));
                    }
                }
                for (int ring = 0; ring < 2; ring++)
                {
                    for (int corner = 0; corner < 7; corner++)
                    {
                        int c = start + ring * 7 + corner;
                        int n = start + ring * 7 + (corner + 1) % 7;
                        indices.Add(c); indices.Add(c + 7); indices.Add(n);
                        indices.Add(n); indices.Add(c + 7); indices.Add(n + 7);
                    }
                }
                for (int corner = 1; corner < 6; corner++)
                {
                    indices.Add(start + 14); indices.Add(start + 14 + corner + 1); indices.Add(start + 14 + corner);
                }
                var points = new Vector3[21];
                vertices.CopyTo(start, points, 0, points.Length);
                var faces = new int[indices.Count - firstIndex];
                for (int index = 0; index < faces.Length; index++)
                {
                    faces[index] = indices[firstIndex + index] - start;
                }
                var contact = new Mesh { name = "RockContact_Runtime" };
                contact.vertices = points;
                contact.triangles = faces;
                contact.RecalculateBounds();
                colliders.Add(contact);

            }
            var mesh = new Mesh { name = "RockyCoast_Runtime" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            collisionMeshes = colliders.ToArray();
            return mesh;
        }
    }
}
