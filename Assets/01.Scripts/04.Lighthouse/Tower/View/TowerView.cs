using Lighthouse.Tower.Net;
using UnityEngine;

namespace Lighthouse.Tower.View
{
    // Shows each slot's module and the beam on the tower model. Follows the synced slots only.
    [DisallowMultipleComponent]
    public sealed class TowerView : MonoBehaviour
    {
        [SerializeField]
        private TowerNet _tower;
        [Tooltip("Shown while the slot with the same index is filled.")]
        [SerializeField]
        private GameObject[] _slotVisuals = new GameObject[0];
        [Tooltip("Shown once every slot is filled.")]
        [SerializeField]
        private GameObject _completeVisual;

        private void OnEnable()
        {
            if (_tower)
            {
                _tower.Changed += HandleTowerChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_tower)
            {
                _tower.Changed -= HandleTowerChanged;
            }
        }

        private void HandleTowerChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!_tower)
            {
                return;
            }

            for (int i = 0; i < _slotVisuals.Length; i++)
            {
                if (_slotVisuals[i])
                {
                    _slotVisuals[i].SetActive(_tower.IsInstalled(i));
                }
            }

            if (_completeVisual)
            {
                _completeVisual.SetActive(_tower.IsComplete);
            }
        }
    }
}
