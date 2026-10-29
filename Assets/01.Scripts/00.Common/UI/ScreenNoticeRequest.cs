using UnityEngine;

namespace Lighthouse.Common.UI
{
    // One line for ScreenNotice. Text is required; the icon falls back to the prefab's default, and the gauge shows
    // only when WithGauge was used. A value type so a caller builds it without allocating.
    public readonly struct ScreenNoticeRequest
    {
        public readonly string Text;
        public readonly ScreenNoticeStyle Style;
        // Null uses the prefab's default icon.
        public readonly Sprite Icon;
        public readonly bool HasGauge;
        public readonly float GaugeFrom;
        public readonly float GaugeTo;
        // Floats up beside the gauge, e.g. "+12.5%". Null or empty shows nothing.
        public readonly string Gain;

        public ScreenNoticeRequest(string text, ScreenNoticeStyle style = ScreenNoticeStyle.Normal, Sprite icon = null)
            : this(text, style, icon, false, 0f, 0f, null)
        {
        }

        private ScreenNoticeRequest(string text, ScreenNoticeStyle style, Sprite icon, bool hasGauge, float from, float to, string gain)
        {
            Text = text;
            Style = style;
            Icon = icon;
            HasGauge = hasGauge;
            GaugeFrom = from;
            GaugeTo = to;
            Gain = gain;
        }

        // Same line with a 0..1 gauge filling from `from` to `to`.
        public ScreenNoticeRequest WithGauge(float from, float to, string gain = null)
        {
            return new ScreenNoticeRequest(Text, Style, Icon, true, Mathf.Clamp01(from), Mathf.Clamp01(to), gain);
        }
    }
}
