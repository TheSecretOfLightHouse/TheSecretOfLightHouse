using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Tower.View
{
    // One lighthouse slot on the install screen. Installed slots and the slot an install would fill get the
    // highlighted frame; the rest show their module faintly so players know what goes there.
    [DisallowMultipleComponent]
    public sealed class TowerSlotView : MonoBehaviour
    {
        public enum State
        {
            Locked,
            Open,
            Target,
            Installed
        }

        [SerializeField]
        private Image _frame;
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private GameObject _lock;
        [SerializeField]
        private GameObject _hereMarker;
        [SerializeField]
        private Sprite _normalSprite;
        [SerializeField]
        private Sprite _highlightSprite;
        [SerializeField, Range(0f, 1f)]
        private float _faintAlpha = 0.35f;

        public void Show(Sprite icon, State state)
        {
            bool isHighlighted = state == State.Installed || state == State.Target;
            _frame.sprite = isHighlighted ? _highlightSprite : _normalSprite;
            // The target slot is drawn empty, waiting for the carried module.
            _icon.sprite = icon;
            _icon.enabled = icon && state != State.Target;
            _icon.color = new Color(1f, 1f, 1f, state == State.Installed ? 1f : _faintAlpha);
            _lock.SetActive(state == State.Locked);
            _hereMarker.SetActive(state == State.Target);
        }
    }
}
