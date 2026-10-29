using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Common.UI
{
    // Selection outline drawn as a mesh around the visible edge of a button's art: a crisp stroke and a soft glow
    // outside it, with the art's own corner radius. Image rings come from other mockups with their own fixed corners
    // and margins, so they never hug every button; this one is fitted per button with _padding and _cornerRadius.
    // Stretch it over the button and use it as UiButtonFeedback's highlight: the graphic's alpha is the reveal, so the
    // outline closes in from _approach while it fades in, then the glow breathes while it stays fully shown.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiButtonOutline : MaskableGraphic
    {
        private const int CornerSegments = 8;
        private const float Feather = 0.75f;

        [Tooltip("Transparent margin of the button art inside this rect: left, bottom, right, top.")]
        [SerializeField]
        private Vector4 _padding;
        [Tooltip("Corner radius of the button art's visible edge.")]
        [SerializeField, Min(0f)]
        private float _cornerRadius = 16f;
        [Tooltip("Space between the art's edge and the stroke.")]
        [SerializeField, Min(0f)]
        private float _gap = 3f;
        [SerializeField, Min(0.5f)]
        private float _thickness = 3f;
        [Tooltip("Extra distance the outline starts from while it fades in.")]
        [SerializeField, Min(0f)]
        private float _approach = 6f;
        [SerializeField]
        private Color _glowColor = new Color(1f, 0.72f, 0.22f, 0.45f);
        [SerializeField, Min(0f)]
        private float _glowWidth = 12f;
        [Tooltip("How much the glow dims at the low point of its breathing. 0 keeps it still.")]
        [SerializeField, Range(0f, 1f)]
        private float _pulse = 0.35f;
        [Tooltip("Breaths per second.")]
        [SerializeField, Min(0.01f)]
        private float _pulseRate = 0.8f;

        private readonly Vector2[] _path = new Vector2[4 * (CornerSegments + 1)];

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private void Update()
        {
            if (_pulse > 0f && color.a > 0.99f)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float reveal = color.a;

            if (reveal <= 0.001f)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            Rect art = Rect.MinMaxRect(rect.xMin + _padding.x, rect.yMin + _padding.y, rect.xMax - _padding.z, rect.yMax - _padding.w);
            float inner = _gap + _approach * (1f - reveal);
            float outer = inner + _thickness;
            float breath = reveal > 0.99f ? 1f - _pulse * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * _pulseRate * 2f * Mathf.PI)) : 1f;
            Color stroke = color;
            Color clear = stroke;
            clear.a = 0f;
            Color glow = _glowColor;
            glow.a *= reveal * breath;
            Color glowClear = glow;
            glowClear.a = 0f;

            // Rings from the inside out; each pair is joined by a band of quads.
            AddRing(vh, art, inner - Feather, clear);
            AddRing(vh, art, inner + Feather * 0.5f, stroke);
            AddRing(vh, art, outer - Feather * 0.5f, stroke);
            AddRing(vh, art, outer + Feather, glow);
            AddRing(vh, art, outer + Feather + _glowWidth * 0.35f, glow * new Color(1f, 1f, 1f, 0.4f));
            AddRing(vh, art, outer + Feather + _glowWidth, glowClear);

            int count = _path.Length;

            for (int ring = 0; ring < 5; ring++)
            {
                int a = ring * count;
                int b = a + count;

                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    vh.AddTriangle(a + i, b + i, b + next);
                    vh.AddTriangle(a + i, b + next, a + next);
                }
            }
        }

        // One rounded-rect loop pushed out from the art's edge by offset, counter-clockwise from the bottom left corner.
        private void AddRing(VertexHelper vh, Rect art, float offset, Color ringColor)
        {
            float radius = Mathf.Min(_cornerRadius, art.width * 0.5f, art.height * 0.5f);
            float bend = Mathf.Max(0f, radius + offset);
            Vector2 min = new Vector2(art.xMin + radius, art.yMin + radius);
            Vector2 max = new Vector2(art.xMax - radius, art.yMax - radius);
            int index = 0;

            index = AddCorner(new Vector2(min.x, min.y), 180f, bend, index);
            index = AddCorner(new Vector2(max.x, min.y), 270f, bend, index);
            index = AddCorner(new Vector2(max.x, max.y), 0f, bend, index);
            AddCorner(new Vector2(min.x, max.y), 90f, bend, index);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = ringColor;

            for (int i = 0; i < _path.Length; i++)
            {
                vertex.position = _path[i];
                vh.AddVert(vertex);
            }
        }

        private int AddCorner(Vector2 center, float startDegrees, float bend, int index)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (startDegrees + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                _path[index++] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * bend;
            }

            return index;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
        }
#endif
    }
}
