using UnityEngine;

namespace Lighthouse.World.MapGeneration.View
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StormWallCurtain : MonoBehaviour
    {
        [SerializeField] private StormWallVisualConfig _config;
        [SerializeField, Range(16, 256)] private int _segments = 128;

        private Mesh _mesh;

        private void Awake()
        {
            if (_config == null || !_config.IsValid
                || !TryGetComponent(out MeshFilter filter))
            {
                Debug.LogError($"[{nameof(StormWallCurtain)}] Config and mesh filter are required.");
                enabled = false;
                return;
            }

            _mesh = BuildTube(_config.FogReferenceRadius, Mathf.Max(16, _segments));
            filter.sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private static Mesh BuildTube(float radius, int segments)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[i * 2] = new Vector3(x, 0f, z);
                vertices[i * 2 + 1] = new Vector3(x, 1f, z);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "StormWallCurtain" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(radius * 2f, 1f, radius * 2f));
            return mesh;
        }
    }
}
