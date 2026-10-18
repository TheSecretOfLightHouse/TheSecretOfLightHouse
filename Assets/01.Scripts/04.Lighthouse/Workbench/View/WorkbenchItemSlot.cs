using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.View
{
    // A grid cell showing an icon and a count, or an empty frame.
    [DisallowMultipleComponent]
    public sealed class WorkbenchItemSlot : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _count;
        [SerializeField]
        private Text _label;

        public void Show(Sprite icon, string label, int count)
        {
            _icon.sprite = icon;
            _icon.enabled = icon;
            _label.text = icon ? string.Empty : label;
            _count.text = count.ToString();
        }

        public void Clear()
        {
            _icon.enabled = false;
            _label.text = string.Empty;
            _count.text = string.Empty;
        }
    }
}
