using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // One ingredient of the recipe on screen: shared progress, the local player's stock and the deposit buttons.
    // There is no take-back button because deposits are final once made (2026-10-17 decision).
    [DisallowMultipleComponent]
    public sealed class WorkbenchIngredientRow : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _name;
        [SerializeField]
        private RectTransform _fill;
        [SerializeField]
        private Text _count;
        [SerializeField]
        private Text _held;
        [SerializeField]
        private GameObject _doneMark;
        [SerializeField]
        private Button _addOneButton;
        [SerializeField]
        private Button _addAllButton;
        [SerializeField]
        private Button _selectButton;
        [SerializeField]
        private Image _selection;
        [SerializeField]
        private bool _isShortageTinted;
        [SerializeField]
        private Color _shortageColor = new Color(0.85f, 0.3f, 0.22f);
        [SerializeField]
        private Color _sufficientColor = new Color(0.96f, 0.91f, 0.76f);
        [Tooltip("Share of the bar filled per second when progress goes up. Drops show at once.")]
        [SerializeField]
        private float _fillSpeed = 1.6f;

        private int _resourceId;
        private Action<int, bool> _depositClicked;
        private Action<int> _selected;
        private float _shownRatio;
        private float _targetRatio;

        public int ResourceId => _resourceId;
        public Image Icon => _icon;
        public RectTransform CountRect => _count.rectTransform;
        public Text Count => _count;
        // Null when the row deposits through the screen's own button instead (sketch layout).
        public RectTransform AddOneRect => _addOneButton && _addOneButton.gameObject.activeInHierarchy ? (RectTransform)_addOneButton.transform : null;

        private void Awake()
        {
            if (_addOneButton)
            {
                _addOneButton.onClick.AddListener(HandleAddOneClicked);
            }

            if (_addAllButton)
            {
                _addAllButton.onClick.AddListener(HandleAddAllClicked);
            }

            if (_selectButton)
            {
                _selectButton.onClick.AddListener(HandleSelected);
            }
        }

        private void Update()
        {
            if (!_fill || _shownRatio == _targetRatio)
            {
                return;
            }

            _shownRatio = Mathf.MoveTowards(_shownRatio, _targetRatio, _fillSpeed * Time.unscaledDeltaTime);
            ApplyFill();
        }

        public void Bind(int resourceId, string displayName, Sprite icon, Action<int, bool> depositClicked)
        {
            Bind(resourceId, displayName, icon, depositClicked, null);
        }

        public void Bind(int resourceId, string displayName, Sprite icon, Action<int, bool> depositClicked, Action<int> selected)
        {
            _resourceId = resourceId;
            _depositClicked = depositClicked;
            _selected = selected;

            if (_name)
            {
                _name.text = displayName;
            }

            _icon.sprite = icon;
            _icon.enabled = icon;
        }

        public void SetProgress(int deposited, int required, int held, bool isOpen)
        {
            _targetRatio = required > 0 ? Mathf.Clamp01((float)deposited / required) : 0f;

            // Only gains climb; a drop (crafted or withdrawn) is not something to show off.
            if (_targetRatio < _shownRatio)
            {
                SnapFill();
            }

            _count.text = $"{deposited} / {required}";

            if (_held)
            {
                _held.text = $"보유 {held}";
            }

            bool isFull = deposited >= required;

            // Red only when there is nothing to put in (2026-10-30): holding fewer than still missing can still go in.
            if (_isShortageTinted)
            {
                _count.color = isFull || held > 0 ? _sufficientColor : _shortageColor;
            }

            if (_doneMark)
            {
                _doneMark.SetActive(isFull);
            }

            bool canDeposit = isOpen && !isFull && held > 0;

            if (_addOneButton)
            {
                _addOneButton.interactable = canDeposit;
            }

            if (_addAllButton)
            {
                _addAllButton.interactable = canDeposit;
            }
        }

        // For a freshly shown recipe, whose bar should not climb from where the previous one stood.
        public void SnapFill()
        {
            _shownRatio = _targetRatio;
            ApplyFill();
        }

        public void SetSelected(bool isSelected)
        {
            if (_selection)
            {
                _selection.enabled = isSelected;
            }
        }

        private void ApplyFill()
        {
            if (_fill)
            {
                _fill.anchorMax = new Vector2(_shownRatio, 1f);
                _fill.gameObject.SetActive(_shownRatio > 0f);
            }
        }

        private void HandleSelected()
        {
            _selected?.Invoke(_resourceId);
        }

        private void HandleAddOneClicked()
        {
            _depositClicked?.Invoke(_resourceId, false);
        }

        private void HandleAddAllClicked()
        {
            _depositClicked?.Invoke(_resourceId, true);
        }
    }
}
