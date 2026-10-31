using UnityEngine;

namespace Lighthouse.UI.Hud
{
    // Keeps right-side HUD groups as far from the right edge of the screen as the role/boat panel is from the left
    // (2026-10-31), so both sides mirror each other. The groups live on canvases that scale differently from the panel's
    // (match width vs. match 0.5), so the gap is carried over in screen pixels and follows any resolution. Positions are
    // set at runtime so the shared HUD prefabs stay as they are; a scene without this component keeps their own spacing.
    [DisallowMultipleComponent]
    public sealed class HudEdgeAlign : MonoBehaviour
    {
        [Tooltip("The bottom-left role/boat panel; its gap from the left edge of the screen is the one copied.")]
        [SerializeField]
        private RectTransform _edgePanel;
        [Tooltip("Right-anchored groups whose right edge moves to the same gap from the right edge of the screen.")]
        [SerializeField]
        private RectTransform[] _rightGroups = new RectTransform[0];

        private Canvas _edgeCanvas;
        private Canvas[] _groupCanvases;
        private float _appliedGap = -1f;

        private void Start()
        {
            _edgeCanvas = _edgePanel ? _edgePanel.GetComponentInParent<Canvas>(true).rootCanvas : null;
            _groupCanvases = new Canvas[_rightGroups.Length];

            for (int i = 0; i < _rightGroups.Length; i++)
            {
                _groupCanvases[i] = _rightGroups[i] ? _rightGroups[i].GetComponentInParent<Canvas>(true).rootCanvas : null;
            }
        }

        // The canvas scalers update their scale in Update, so the gap is read after them and applied only when it changes.
        private void LateUpdate()
        {
            if (!_edgeCanvas)
            {
                return;
            }

            // The panel is anchored to the left edge of a full-screen parent, so its left edge is its x minus the pivot part.
            float edgeLeft = _edgePanel.anchoredPosition.x - _edgePanel.pivot.x * _edgePanel.sizeDelta.x;
            float gap = edgeLeft * _edgeCanvas.scaleFactor;

            if (Mathf.Approximately(gap, _appliedGap))
            {
                return;
            }

            _appliedGap = gap;

            for (int i = 0; i < _rightGroups.Length; i++)
            {
                RectTransform group = _rightGroups[i];
                Canvas canvas = _groupCanvases[i];

                if (!group || !canvas || canvas.scaleFactor <= 0f)
                {
                    continue;
                }

                float groupGap = gap / canvas.scaleFactor;
                Vector2 position = group.anchoredPosition;
                position.x = -groupGap - (1f - group.pivot.x) * group.sizeDelta.x;
                group.anchoredPosition = position;
            }
        }
    }
}
