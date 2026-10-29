using UnityEngine;

namespace Lighthouse.Player.Inventory.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryFrameGraphic : UnityEngine.UI.MaskableGraphic
    {
        private Color _border = new Color(0.95f, 0.88f, 0.73f);
        private float _thickness = 3f;
        private float _radius = 14f;

        public void Configure(Color border, float thickness = 3f, float radius = 14f)
        {
            _border = border;
            _thickness = thickness;
            _radius = radius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(_radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            const int Count = 40;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int i = 0; i < Count; i++)
            {
                int corner = i / 10;
                float angle = (corner * 90f + i % 10 / 9f * 90f) * Mathf.Deg2Rad;
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // A fixed slight irregularity keeps the outline handmade without shimmering between frames.
                float wobble = Mathf.Sin(i * 2.13f) * 0.55f;
                Vector2 outer = center + direction * (radius + wobble);
                Vector2 inner = outer - new Vector2(Mathf.Sign(outer.x - rect.center.x), Mathf.Sign(outer.y - rect.center.y)) * _thickness;
                mesh.AddVert(outer, _border, Vector2.zero);
                mesh.AddVert(inner, color, Vector2.zero);
            }
            for (int i = 0; i < Count; i++)
            {
                int outer = 1 + i * 2;
                int next = 1 + (i + 1) % Count * 2;
                mesh.AddTriangle(outer, next, next + 1);
                mesh.AddTriangle(outer, next + 1, outer + 1);
                mesh.AddTriangle(0, outer + 1, next + 1);
            }
        }
    }
}
