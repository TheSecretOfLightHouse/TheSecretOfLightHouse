using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    public static class DockSeabedMesh
    {
        private const float Depth = 0.4f;
        private const float SideMargin = 1f;
        private const float BlendDistance = 3f;

        public static Mesh Build(MeshFilter ground, Transform island, out Renderer connection)
        {
            connection = null;
            BoxCollider dock = null;
            BoxCollider[] colliders = island.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].name == "Walkable_Dock_Transition")
                {
                    dock = colliders[i];
                    break;
                }
            }
            if (dock == null || ground.sharedMesh == null)
            {
                return null;
            }

            connection = dock.GetComponent<Renderer>();
            Vector3 center = dock.transform.TransformPoint(dock.center);
            Vector3 right = dock.transform.right;
            Vector3 forward = dock.transform.forward;
            float halfWidth = dock.size.x * dock.transform.TransformVector(Vector3.right).magnitude * 0.5f;
            float halfLength = dock.size.z * dock.transform.TransformVector(Vector3.forward).magnitude * 0.5f;
            Vector3[] vertices = ground.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = ground.transform.TransformPoint(vertices[i]);
                Vector3 offset = world - center;
                float side = Mathf.Abs(Vector3.Dot(offset, right));
                float along = Vector3.Dot(offset, forward);
                float sideWeight = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(halfWidth + SideMargin, halfWidth + SideMargin + BlendDistance, side));
                float inlandWeight = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(-halfLength, -halfLength + SideMargin, along));
                float offshoreWeight = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(halfLength + SideMargin, halfLength + SideMargin + BlendDistance, along));
                float alongWeight = inlandWeight * offshoreWeight;
                world.y = Mathf.Lerp(world.y, Mathf.Min(world.y, -Depth), sideWeight * alongWeight);
                vertices[i] = ground.transform.InverseTransformPoint(world);
            }
            Mesh mesh = Object.Instantiate(ground.sharedMesh);
            mesh.name = "DockSeabed_Runtime";
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
