using System;
using Lighthouse.Workbench.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // One recipe in the workbench list. The screen decides the state; the card only draws it.
    [DisallowMultipleComponent]
    public sealed class WorkbenchRecipeCard : MonoBehaviour
    {
        [SerializeField]
        private Button _button;
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _name;
        [SerializeField]
        private Text _detail;
        [SerializeField]
        private GameObject _badge;
        [SerializeField]
        private Text _badgeText;
        [SerializeField]
        private CanvasGroup _group;
        [SerializeField]
        private Sprite _normalSprite;
        [SerializeField]
        private Sprite _selectedSprite;
        [SerializeField]
        private Color _normalTextColor = new Color(0.86f, 0.83f, 0.76f);
        [SerializeField]
        private Color _selectedTextColor = new Color(0.17f, 0.12f, 0.08f);
        [SerializeField]
        private Color _normalDetailColor = new Color(0.55f, 0.6f, 0.68f);
        [SerializeField]
        private Color _selectedDetailColor = new Color(0.33f, 0.25f, 0.17f);
        [SerializeField, Range(0f, 1f)]
        private float _disabledAlpha = 0.45f;

        private ModuleRecipe _recipe;
        private Action<ModuleRecipe> _clicked;

        public ModuleRecipe Recipe => _recipe;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
        }

        public void Bind(ModuleRecipe recipe, string detail, Sprite fallbackIcon, Action<ModuleRecipe> clicked)
        {
            _recipe = recipe;
            _clicked = clicked;
            _name.text = recipe.DisplayName;
            _detail.text = detail;
            _icon.sprite = recipe.Icon ? recipe.Icon : fallbackIcon;
            _icon.enabled = _icon.sprite;
        }

        public void SetState(bool isSelected, bool isInteractable, string badge)
        {
            _background.sprite = isSelected ? _selectedSprite : _normalSprite;
            _name.color = isSelected ? _selectedTextColor : _normalTextColor;
            _detail.color = isSelected ? _selectedDetailColor : _normalDetailColor;
            _button.interactable = isInteractable;
            _group.alpha = isSelected || isInteractable ? 1f : _disabledAlpha;
            bool hasBadge = !string.IsNullOrEmpty(badge);
            _badge.SetActive(hasBadge);

            if (hasBadge)
            {
                _badgeText.text = badge;
            }
        }

        private void HandleClicked()
        {
            if (_recipe)
            {
                _clicked?.Invoke(_recipe);
            }
        }
    }
}
