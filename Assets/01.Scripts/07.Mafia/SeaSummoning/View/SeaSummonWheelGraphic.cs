using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SeaSummonWheelGraphic : MaskableGraphic
    {
        private const int SlotCount = 7;
        private const int ArcSteps = 64;
        private const float EdgeFade = 1.5f;
        private int _hover = -1;
        private bool[] _enabledSlots = new bool[SlotCount];
        private bool _isPending;

        public void SetState(int hover, bool[] enabledSlots, bool isPending)
        {
            _hover = hover;
            _enabledSlots = enabledSlots;
            _isPending = isPending;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            Color edge = new Color(0.075f, 0.035f, 0.015f);
            Color gold = new Color(0.53f, 0.29f, 0.075f);
            Color ivory = new Color(1f, 0.965f, 0.86f);
            Disc(vh, radius, edge);
            Disc(vh, radius * 0.983f, gold);
            Disc(vh, radius * 0.972f, edge);
            Disc(vh, radius * 0.956f, ivory);

            float segmentAngle = 360f / SlotCount;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                float middle = slot * segmentAngle;
                bool enabled = _enabledSlots != null && _enabledSlots[slot] && !_isPending;
                bool selected = enabled && slot == _hover;
                Color outline = new Color(0.07f, 0.035f, 0.018f);
                Color inset = enabled ? new Color(0.16f, 0.075f, 0.042f) : new Color(0.09f, 0.04f, 0.025f);
                Color inner = enabled ? new Color(0.23f, 0.12f, 0.075f) : new Color(0.12f, 0.06f, 0.04f);
                Color outer = enabled ? new Color(0.31f, 0.17f, 0.105f) : new Color(0.17f, 0.09f, 0.055f);
                float halfAngle = segmentAngle * 0.5f;
                // The ivory divider is the same width as the ivory ring around the center.
                Sector(vh, radius * 0.414f, radius * 0.938f, middle, halfAngle,
                    radius * 0.014f, outline, outline);
                Sector(vh, radius * 0.418f, radius * 0.934f, middle, halfAngle,
                    radius * 0.018f, inset, inset);
                Sector(vh, radius * 0.433f, radius * 0.919f, middle, halfAngle,
                    radius * 0.033f, inner, outer);
                if (selected)
                {
                    AddSelectionGlow(vh, radius * 0.448f, radius * 0.904f, middle,
                        segmentAngle * 0.5f - 4.8f);
                }
            }
            Disc(vh, radius * 0.425f, ivory);
            Disc(vh, radius * 0.397f, edge);
            Disc(vh, radius * 0.386f, gold);
            Disc(vh, radius * 0.378f, edge);
            Disc(vh, radius * 0.367f, new Color(0.23f, 0.12f, 0.075f));
        }

        private static void AddSelectionGlow(VertexHelper vh, float inner, float outer, float middle, float halfAngle)
        {
            const int radialSteps = 20;
            const int angularSteps = 64;
            int first = vh.currentVertCount;
            for (int r = 0; r <= radialSteps; r++)
            {
                float radial = (float)r / radialSteps;
                float radialFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(radial / 0.2f))
                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radial) / 0.2f));
                float distance = Mathf.Lerp(inner, outer, radial);
                for (int a = 0; a <= angularSteps; a++)
                {
                    float angular = (float)a / angularSteps;
                    float angularFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(angular / 0.17f))
                        * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - angular) / 0.17f));
                    float angle = (middle + Mathf.Lerp(-halfAngle, halfAngle, angular)) * Mathf.Deg2Rad;
                    Color glow = new Color(1f, 0.76f, 0.06f, 0.84f * radialFade * angularFade);
                    vh.AddVert(Point(angle, distance), glow, Vector2.zero);
                    if (r == 0 || a == 0) continue;
                    int current = first + r * (angularSteps + 1) + a;
                    int previousRow = current - (angularSteps + 1);
                    vh.AddTriangle(previousRow - 1, current - 1, current);
                    vh.AddTriangle(previousRow - 1, current, previousRow);
                }
            }
        }

        private static void Disc(VertexHelper vh, float radius, Color color)
        {
            int center = vh.currentVertCount;
            vh.AddVert(Vector2.zero, color, Vector2.zero);
            const int steps = 256;
            for (int i = 0; i <= steps; i++)
            {
                vh.AddVert(Point(i * Mathf.PI * 2f / steps, radius), color, Vector2.zero);
                if (i > 0) vh.AddTriangle(center, center + i, center + i + 1);
            }
            Color transparent = new Color(color.r, color.g, color.b, 0f);
            int fringe = vh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2f / steps;
                vh.AddVert(Point(angle, radius), color, Vector2.zero);
                vh.AddVert(Point(angle, radius + EdgeFade), transparent, Vector2.zero);
                if (i == 0) continue;
                int previous = fringe + (i - 1) * 2;
                vh.AddTriangle(previous, previous + 1, previous + 3);
                vh.AddTriangle(previous, previous + 3, previous + 2);
            }
        }

        private static void Sector(VertexHelper vh, float inner, float outer, float middle, float halfAngle,
            float halfGap, Color innerColor, Color outerColor)
        {
            float centerAngle = middle * Mathf.Deg2Rad;
            float half = halfAngle * Mathf.Deg2Rad;
            float innerHalf = half - Mathf.Asin(Mathf.Clamp(halfGap / inner, 0f, 1f));
            float outerHalf = half - Mathf.Asin(Mathf.Clamp(halfGap / outer, 0f, 1f));
            int first = vh.currentVertCount;
            for (int i = 0; i <= ArcSteps; i++)
            {
                float t = (float)i / ArcSteps;
                float innerAngle = centerAngle + Mathf.Lerp(-innerHalf, innerHalf, t);
                float outerAngle = centerAngle + Mathf.Lerp(-outerHalf, outerHalf, t);
                vh.AddVert(Point(innerAngle, inner), innerColor, Vector2.zero);
                vh.AddVert(Point(outerAngle, outer), outerColor, Vector2.zero);
                if (i == 0) continue;
                int previous = first + (i - 1) * 2;
                vh.AddTriangle(previous, previous + 1, previous + 3);
                vh.AddTriangle(previous, previous + 3, previous + 2);
            }
            Color innerClear = new Color(innerColor.r, innerColor.g, innerColor.b, 0f);
            Color outerClear = new Color(outerColor.r, outerColor.g, outerColor.b, 0f);
            int fringe = vh.currentVertCount;
            for (int i = 0; i <= ArcSteps; i++)
            {
                float t = (float)i / ArcSteps;
                float innerAngle = centerAngle + Mathf.Lerp(-innerHalf, innerHalf, t);
                float outerAngle = centerAngle + Mathf.Lerp(-outerHalf, outerHalf, t);
                vh.AddVert(Point(innerAngle, inner - EdgeFade), innerClear, Vector2.zero);
                vh.AddVert(Point(innerAngle, inner), innerColor, Vector2.zero);
                vh.AddVert(Point(outerAngle, outer), outerColor, Vector2.zero);
                vh.AddVert(Point(outerAngle, outer + EdgeFade), outerClear, Vector2.zero);
                if (i == 0) continue;
                int previous = fringe + (i - 1) * 4;
                vh.AddTriangle(previous, previous + 1, previous + 5);
                vh.AddTriangle(previous, previous + 5, previous + 4);
                vh.AddTriangle(previous + 2, previous + 3, previous + 7);
                vh.AddTriangle(previous + 2, previous + 7, previous + 6);
            }
            AddSideFringe(vh, Point(centerAngle - innerHalf, inner), Point(centerAngle - outerHalf, outer),
                centerAngle - half, -1f, innerColor, outerColor);
            AddSideFringe(vh, Point(centerAngle + innerHalf, inner), Point(centerAngle + outerHalf, outer),
                centerAngle + half, 1f, innerColor, outerColor);
        }

        private static void AddSideFringe(VertexHelper vh, Vector2 a, Vector2 b, float angle, float direction,
            Color innerColor, Color outerColor)
        {
            Vector2 offset = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * (direction * EdgeFade);
            Color innerClear = new Color(innerColor.r, innerColor.g, innerColor.b, 0f);
            Color outerClear = new Color(outerColor.r, outerColor.g, outerColor.b, 0f);
            int first = vh.currentVertCount;
            vh.AddVert(a, innerColor, Vector2.zero);
            vh.AddVert(b, outerColor, Vector2.zero);
            vh.AddVert(b + offset, outerClear, Vector2.zero);
            vh.AddVert(a + offset, innerClear, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }

        private static Vector2 Point(float angle, float radius) => new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
    }
}
