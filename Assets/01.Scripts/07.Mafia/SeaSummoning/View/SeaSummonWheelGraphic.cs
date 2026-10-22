using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SeaSummonWheelGraphic : MaskableGraphic
    {
        private const int SlotCount = 7;
        private int _hover = -1;
        private bool[] _enabledSlots = new bool[SlotCount];
        private bool _isPending;

        public void SetState(int hover, bool[] enabledSlots, bool isPending)
        {
            if (_hover == hover && _enabledSlots == enabledSlots && _isPending == isPending)
            {
                return;
            }
            _hover = hover;
            _enabledSlots = enabledSlots;
            _isPending = isPending;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            float inner = radius * 0.44f;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                bool enabledSlot = _enabledSlots != null && _enabledSlots[slot] && !_isPending;
                Color fill = !enabledSlot ? new Color(0.12f, 0.14f, 0.18f) : slot == _hover
                    ? new Color(0.64f, 0.43f, 0.12f) : new Color(0.16f, 0.23f, 0.31f);
                float start = slot * 360f / SlotCount - 180f / SlotCount + 1f;
                float end = (slot + 1) * 360f / SlotCount - 180f / SlotCount - 1f;
                for (int part = 0; part < 16; part++)
                {
                    float a = Mathf.Lerp(start, end, part / 16f) * Mathf.Deg2Rad;
                    float b = Mathf.Lerp(start, end, (part + 1) / 16f) * Mathf.Deg2Rad;
                    AddQuad(vh, Point(a, inner), Point(a, radius), Point(b, radius), Point(b, inner), fill);
                    AddQuad(vh, Point(a, radius - 2f), Point(a, radius), Point(b, radius), Point(b, radius - 2f),
                        enabledSlot ? new Color(0.76f, 0.78f, 0.73f) : new Color(0.3f, 0.32f, 0.36f));
                }
            }
            Color core = new Color(0.07f, 0.1f, 0.15f);
            for (int i = 0; i < 64; i++)
            {
                int index = vh.currentVertCount;
                vh.AddVert(Vector2.zero, core, Vector2.zero);
                vh.AddVert(Point(i * Mathf.PI / 32f, inner - 5f), core, Vector2.zero);
                vh.AddVert(Point((i + 1) * Mathf.PI / 32f, inner - 5f), core, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
            }
        }

        private static Vector2 Point(float angle, float radius)
        {
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
