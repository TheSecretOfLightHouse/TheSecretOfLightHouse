using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryBoatGraphic : UnityEngine.UI.MaskableGraphic
    {
        private static readonly Vector2[] Hull =
        {
            new Vector2(0.5f, 0.99f), new Vector2(0.76f, 0.82f), new Vector2(0.91f, 0.53f),
            new Vector2(0.86f, 0.08f), new Vector2(0.14f, 0.08f), new Vector2(0.09f, 0.53f), new Vector2(0.24f, 0.82f)
        };

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            AddHull(mesh, 1f, new Color(0.91f, 0.72f, 0.38f));
            AddHull(mesh, 0.93f, new Color(0.22f, 0.12f, 0.07f));
            AddHull(mesh, 0.79f, new Color(0.72f, 0.58f, 0.37f));
            for (int i = 0; i < 7; i++)
            {
                AddQuad(mesh, new Rect(0.28f, 0.19f + i * 0.065f, 0.44f, 0.008f), new Color(0.45f, 0.34f, 0.21f));
            }
            AddQuad(mesh, new Rect(0.34f, 0.29f, 0.32f, 0.25f), new Color(0.94f, 0.9f, 0.76f));
            AddQuad(mesh, new Rect(0.37f, 0.45f, 0.26f, 0.055f), new Color(0.13f, 0.34f, 0.38f));
            AddQuad(mesh, new Rect(0.45f, 0.59f, 0.1f, 0.19f), new Color(0.94f, 0.9f, 0.76f));
        }

        private void AddHull(UnityEngine.UI.VertexHelper mesh, float scale, Color tint)
        {
            int start = mesh.currentVertCount;
            Rect rect = rectTransform.rect;
            mesh.AddVert(new Vector3(rect.center.x, rect.center.y), tint, Vector2.zero);
            for (int i = 0; i < Hull.Length; i++)
            {
                Vector2 point = (Hull[i] - Vector2.one * 0.5f) * scale;
                mesh.AddVert(new Vector3(rect.center.x + point.x * rect.width, rect.center.y + point.y * rect.height), tint, Vector2.zero);
            }
            for (int i = 0; i < Hull.Length; i++)
            {
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Hull.Length);
            }
        }

        private void AddQuad(UnityEngine.UI.VertexHelper mesh, Rect area, Color tint)
        {
            int start = mesh.currentVertCount;
            Rect rect = rectTransform.rect;
            mesh.AddVert(new Vector3(rect.x + area.xMin * rect.width, rect.y + area.yMin * rect.height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.x + area.xMin * rect.width, rect.y + area.yMax * rect.height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.x + area.xMax * rect.width, rect.y + area.yMax * rect.height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.x + area.xMax * rect.width, rect.y + area.yMin * rect.height), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
