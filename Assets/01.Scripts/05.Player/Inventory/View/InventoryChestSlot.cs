using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lighthouse.Player.Inventory.View
{
    public sealed class InventoryChestSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _count;
        [SerializeField] private GameObject _highlight;
        [SerializeField] private InventoryChestScreen _screen;
        [SerializeField] private int _moduleIndex = -1;
        [SerializeField] private float _liftHeight = 10f;

        private bool _isHovered;
        private bool _isSelected;

        public void SetContent(Sprite icon, string count)
        {
            _icon.sprite = icon;
            _icon.enabled = icon;
            _count.text = count;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RefreshHighlight();
        }

        private void OnDisable()
        {
            _isHovered = false;
            _icon.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            float target = _isHovered && _icon.enabled ? _liftHeight : 0f;
            Vector2 position = _icon.rectTransform.anchoredPosition;
            position.y = Mathf.MoveTowards(position.y, target, Time.unscaledDeltaTime * 100f);
            _icon.rectTransform.anchoredPosition = position;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshHighlight();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_moduleIndex >= 0 && eventData.button == PointerEventData.InputButton.Left)
            {
                _screen.SelectModule(_moduleIndex);
            }
        }

        private void RefreshHighlight()
        {
            _highlight.SetActive(_isSelected || _isHovered && _icon.enabled);
        }
    }
}
