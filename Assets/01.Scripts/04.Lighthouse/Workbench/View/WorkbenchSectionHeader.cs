using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // A heading in the recipe list ("등대 모듈", "배 모듈") with an optional icon.
    [DisallowMultipleComponent]
    public sealed class WorkbenchSectionHeader : MonoBehaviour
    {
        [SerializeField]
        private Text _label;
        [SerializeField]
        private Image _icon;

        public void Bind(string title, Sprite icon)
        {
            _label.text = title;

            if (_icon)
            {
                _icon.sprite = icon;
                _icon.enabled = icon;
            }
        }
    }
}
