using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lighthouse.Common.UI
{
    // Hover, press and keyboard-selection feedback for any Selectable: a small scale punch and an optional
    // highlight graphic (for example a ring) that fades in. Colour states stay on the Selectable's own transition.
    // Pointer clicks also select a button in uGUI; that selection is ignored so the ring does not stick after a click.
    [DisallowMultipleComponent]
    public sealed class UiButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Defaults to the Selectable on this object.")]
        [SerializeField]
        private Selectable _selectable;
        [Tooltip("Scaled transform. Defaults to this object.")]
        [SerializeField]
        private RectTransform _target;
        [SerializeField]
        private float _hoverScale = 1.04f;
        [SerializeField]
        private float _pressScale = 0.96f;
        [Tooltip("Optional graphic shown while hovered or selected by keyboard/gamepad.")]
        [SerializeField]
        private Graphic _highlight;
        [Tooltip("Higher is snappier. Uses unscaled time so paused menus still respond.")]
        [SerializeField]
        private float _sharpness = 18f;

        private bool _isHovered;
        private bool _isPressed;
        private bool _isNavSelected;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (!_selectable)
            {
                _selectable = GetComponent<Selectable>();
            }

            if (!_target)
            {
                _target = (RectTransform)transform;
            }

            _baseScale = _target.localScale;
        }

        private void OnEnable()
        {
            SetHighlightAlpha(0f);
        }

        private void Update()
        {
            bool isActive = !_selectable || _selectable.IsInteractable();
            float scale = !isActive ? 1f : _isPressed ? _pressScale : _isHovered || _isNavSelected ? _hoverScale : 1f;
            float highlight = isActive && (_isHovered || _isNavSelected) ? 1f : 0f;
            Vector3 targetScale = _baseScale * scale;
            bool isScaleSettled = (_target.localScale - targetScale).sqrMagnitude < 1e-6f;
            bool isHighlightSettled = !_highlight || Mathf.Abs(_highlight.color.a - highlight) < 0.002f;

            if (isScaleSettled && isHighlightSettled)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-_sharpness * Time.unscaledDeltaTime);
            _target.localScale = Vector3.Lerp(_target.localScale, targetScale, t);

            if (_highlight)
            {
                SetHighlightAlpha(Mathf.Lerp(_highlight.color.a, highlight, t));
            }
        }

        private void OnDisable()
        {
            // Hidden dialogs and screens come back in their resting state.
            _isHovered = false;
            _isPressed = false;
            _isNavSelected = false;
            _target.localScale = _baseScale;
            SetHighlightAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isPressed = true;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isNavSelected = !(eventData is PointerEventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isNavSelected = false;
        }

        private void SetHighlightAlpha(float alpha)
        {
            if (!_highlight)
            {
                return;
            }

            Color color = _highlight.color;
            color.a = alpha;
            _highlight.color = color;
        }
    }
}
