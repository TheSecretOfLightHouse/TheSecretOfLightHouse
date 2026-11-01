using UnityEngine;

namespace Lighthouse.World.Beacon.View
{
    public static class BeaconConeMesh
    {
        private const int Segments = 24;
        private const int RingSize = Segments + 1;

        // Open cone with the apex at the origin, a unit radius at z = 1, uv.y = 0 at the apex and 1 at the far end.
        public static Mesh Create()
        {
            var vertices = new Vector3[RingSize * 2];
            var normals = new Vector3[RingSize * 2];
            var uvs = new Vector2[RingSize * 2];
            var triangles = new int[Segments * 6];

            for (int i = 0; i < RingSize; i++)
            {
                float u = (float)i / Segments;
                float angle = u * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                vertices[i] = Vector3.zero;
                vertices[RingSize + i] = radial + Vector3.forward;
                normals[i] = radial;
                normals[RingSize + i] = radial;
                uvs[i] = new Vector2(u, 0f);
                uvs[RingSize + i] = new Vector2(u, 1f);
            }

            for (int i = 0; i < Segments; i++)
            {
                int t = i * 6;
                triangles[t] = i;
                triangles[t + 1] = RingSize + i;
                triangles[t + 2] = RingSize + i + 1;
                triangles[t + 3] = i;
                triangles[t + 4] = RingSize + i + 1;
                triangles[t + 5] = i + 1;
            }

            var mesh = new Mesh { name = "BeaconCone" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));
            return mesh;
        }
    }
}
