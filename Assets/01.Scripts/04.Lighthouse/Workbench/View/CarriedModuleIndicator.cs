using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // Small corner tag showing the module this player carries (UI effects 2026-10-26), so they need not open the
    // lighthouse screen to check. It reads OwnCarriedRecipe, which only the carrier is ever told (holders are private,
    // 2026-10-21), so other players' screens never show it.
    [DisallowMultipleComponent]
    public sealed class CarriedModuleIndicator : MonoBehaviour
    {
        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private GameObject _root;
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _text;
        [SerializeField]
        private string _format = "들고 있는 부품: {0}";

        private int _shownRecipe = -1;

        private void Awake()
        {
            _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_workbench)
            {
                _workbench.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_workbench)
            {
                _workbench.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            int recipeId = _workbench ? _workbench.OwnCarriedRecipe : 0;

            if (recipeId == _shownRecipe)
            {
                return;
            }

            _shownRecipe = recipeId;
            ModuleRecipe recipe = null;
            bool isCarrying = recipeId != 0 && _workbench.Catalog && _workbench.Catalog.TryGetById(recipeId, out recipe);
            _root.SetActive(isCarrying);

            if (!isCarrying)
            {
                return;
            }

            if (_text)
            {
                _text.text = string.Format(_format, recipe.DisplayName);
            }

            if (_icon)
            {
                _icon.sprite = recipe.Icon;
                _icon.enabled = recipe.Icon;
            }
        }
    }
}
