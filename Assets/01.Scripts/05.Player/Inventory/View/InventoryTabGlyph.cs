using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryTabGlyph : UnityEngine.UI.MaskableGraphic
    {
        private int _kind;

        public void Configure(int kind)
        {
            _kind = kind;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (_kind == 0)
            {
                Line(mesh, new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.2f));
                Line(mesh, new Vector2(0.75f, 0.2f), new Vector2(0.8f, 0.65f));
                Line(mesh, new Vector2(0.8f, 0.65f), new Vector2(0.2f, 0.65f));
                Line(mesh, new Vector2(0.2f, 0.65f), new Vector2(0.25f, 0.2f));
                Ring(mesh, new Vector2(0.5f, 0.7f), 0.17f, 0f, 180f);
                Line(mesh, new Vector2(0.3f, 0.48f), new Vector2(0.7f, 0.48f));
                Line(mesh, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.4f));
            }
            else if (_kind == 1)
            {
                Line(mesh, new Vector2(0.23f, 0.2f), new Vector2(0.66f, 0.64f));
                Line(mesh, new Vector2(0.31f, 0.16f), new Vector2(0.72f, 0.58f));
                Ring(mesh, new Vector2(0.7f, 0.7f), 0.2f, 80f, 355f);
                Ring(mesh, new Vector2(0.27f, 0.2f), 0.06f, 0f, 360f);
            }
            else
            {
                Ring(mesh, new Vector2(0.5f, 0.5f), 0.28f, 0f, 360f);
                Ring(mesh, new Vector2(0.5f, 0.5f), 0.07f, 0f, 360f);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(mesh, Vector2.one * 0.5f + direction * 0.09f, Vector2.one * 0.5f + direction * 0.42f);
                }
            }
        }

        private void Ring(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, float from, float to)
        {
            for (int i = 0; i < 24; i++)
            {
                float first = Mathf.Lerp(from, to, i / 24f) * Mathf.Deg2Rad;
                float second = Mathf.Lerp(from, to, (i + 1) / 24f) * Mathf.Deg2Rad;
                Line(mesh, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius);
            }
        }

        private void Line(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to)
        {
            Rect rect = rectTransform.rect;
            Vector2 a = rect.min + Vector2.Scale(from, rect.size);
            Vector2 b = rect.min + Vector2.Scale(to, rect.size);
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 1.2f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
