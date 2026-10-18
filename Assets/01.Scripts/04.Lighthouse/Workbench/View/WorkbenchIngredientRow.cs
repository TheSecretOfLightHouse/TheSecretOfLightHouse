using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // One ingredient of the current recipe: shared progress, the local player's stock and the deposit buttons.
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

        private int _resourceId;
        private Action<int, bool> _depositClicked;

        private void Awake()
        {
            _addOneButton.onClick.AddListener(HandleAddOneClicked);
            _addAllButton.onClick.AddListener(HandleAddAllClicked);
        }

        public void Bind(int resourceId, string displayName, Sprite icon, Action<int, bool> depositClicked)
        {
            _resourceId = resourceId;
            _depositClicked = depositClicked;
            _name.text = displayName;
            _icon.sprite = icon;
            _icon.enabled = icon;
        }

        public void SetProgress(int deposited, int required, int held)
        {
            float ratio = required > 0 ? Mathf.Clamp01((float)deposited / required) : 0f;
            _fill.anchorMax = new Vector2(ratio, 1f);
            _fill.gameObject.SetActive(ratio > 0f);
            _count.text = $"{deposited} / {required}";
            _held.text = $"보유 {held}";
            bool isFull = deposited >= required;
            _doneMark.SetActive(isFull);
            bool canDeposit = !isFull && held > 0;
            _addOneButton.interactable = canDeposit;
            _addAllButton.interactable = canDeposit;
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
