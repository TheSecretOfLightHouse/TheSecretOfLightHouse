using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // A grid cell showing an icon and a count, or an empty frame. Stored module cells also carry a name and a
    // button (take, 2026-10-20); resource cells leave both unassigned.
    [DisallowMultipleComponent]
    public sealed class WorkbenchItemSlot : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _count;
        [SerializeField]
        private Text _label;
        [Tooltip("Optional name always shown above the icon. Without it the label stands in only when there is no icon.")]
        [SerializeField]
        private Text _name;
        [Tooltip("Optional button under the cell.")]
        [SerializeField]
        private Button _actionButton;
        [SerializeField]
        private Image _selection;

        private Action<int> _actionClicked;
        private int _actionId;

        public RectTransform IconRect => _icon.rectTransform;
        public RectTransform CountRect => _count.rectTransform;

        private void Awake()
        {
            if (_actionButton)
            {
                _actionButton.onClick.AddListener(HandleActionClicked);
            }
        }

        public void Show(Sprite icon, string label, int count)
        {
            _icon.sprite = icon;
            _icon.enabled = icon;
            _label.text = _name || icon ? string.Empty : label;
            _count.text = count.ToString();

            if (_name)
            {
                _name.text = label;
            }
        }

        public void Clear()
        {
            _icon.enabled = false;
            _label.text = string.Empty;
            _count.text = string.Empty;

            if (_name)
            {
                _name.text = string.Empty;
            }

            SetAction(0, false, null);
            SetSelected(false);
        }

        public void SetSelected(bool isSelected)
        {
            if (_selection)
            {
                _selection.enabled = isSelected;
            }
        }

        // id is handed back to clicked. A null clicked hides the button.
        public void SetAction(int id, bool isInteractable, Action<int> clicked)
        {
            _actionId = id;
            _actionClicked = clicked;

            if (_actionButton)
            {
                _actionButton.gameObject.SetActive(clicked != null);
                _actionButton.interactable = isInteractable;
            }
        }

        private void HandleActionClicked()
        {
            _actionClicked?.Invoke(_actionId);
        }
    }
}
