using Lighthouse.Tower.Net;
using UnityEngine;

namespace Lighthouse.Tower.View
{
    // Shows each slot's module and the beam on the tower model. Follows the synced slots only.
    // A slot installed during play drops into place from above (2026-10-27); the state a late joiner starts with, and
    // anything shown on enable, is set at once.
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
        [Tooltip("World height a newly installed slot's model falls from.")]
        [SerializeField, Min(0f)]
        private float _dropHeight = 1.5f;
        [SerializeField, Min(0.01f)]
        private float _dropSeconds = 0.45f;

        private Vector3[] _restPositions = new Vector3[0];
        // Seconds into each slot's drop, below zero when it is not falling.
        private float[] _dropElapsed = new float[0];
        private int _dropCount;

        private void Awake()
        {
            _restPositions = new Vector3[_slotVisuals.Length];
            _dropElapsed = new float[_slotVisuals.Length];

            for (int i = 0; i < _slotVisuals.Length; i++)
            {
                _restPositions[i] = _slotVisuals[i] ? _slotVisuals[i].transform.localPosition : Vector3.zero;
                _dropElapsed[i] = -1f;
            }
        }

        private void OnEnable()
        {
            if (_tower)
            {
                _tower.Changed += HandleTowerChanged;
                _tower.SlotInstalled += HandleSlotInstalled;
            }

            Refresh();
        }

        private void Update()
        {
            if (_dropCount == 0)
            {
                return;
            }

            for (int i = 0; i < _dropElapsed.Length; i++)
            {
                if (_dropElapsed[i] < 0f)
                {
                    continue;
                }

                _dropElapsed[i] += Time.deltaTime;
                float t = _dropElapsed[i] / _dropSeconds;

                if (t >= 1f)
                {
                    SettleDrop(i);
                    continue;
                }

                // Falls faster and faster, like a part let go of.
                SetDropOffset(i, 1f - t * t);
            }
        }

        private void OnDisable()
        {
            if (_tower)
            {
                _tower.Changed -= HandleTowerChanged;
                _tower.SlotInstalled -= HandleSlotInstalled;
            }

            for (int i = 0; i < _dropElapsed.Length; i++)
            {
                if (_dropElapsed[i] >= 0f)
                {
                    SettleDrop(i);
                }
            }
        }

        public void Bind(TowerNet tower, GameObject[] slotVisuals, GameObject completeVisual)
        {
            if (isActiveAndEnabled && _tower)
            {
                _tower.Changed -= HandleTowerChanged;
            }

            _tower = tower;
            _slotVisuals = slotVisuals;
            _completeVisual = completeVisual;

            if (isActiveAndEnabled && _tower)
            {
                _tower.Changed += HandleTowerChanged;
            }

            Refresh();
        }

        private void HandleTowerChanged()
        {
            Refresh();
        }

        // Comes before the Changed that shows the slot, so the model is already up high when it appears.
        private void HandleSlotInstalled(int slot)
        {
            if (slot < 0 || slot >= _slotVisuals.Length || !_slotVisuals[slot] || _dropHeight <= 0f)
            {
                return;
            }

            if (_dropElapsed[slot] < 0f)
            {
                _dropCount++;
            }

            _dropElapsed[slot] = 0f;
            SetDropOffset(slot, 1f);
        }

        private void SetDropOffset(int slot, float share)
        {
            Transform visual = _slotVisuals[slot].transform;
            // Up in the world, whatever the parent's rotation or scale.
            Vector3 up = visual.parent ? visual.parent.InverseTransformVector(Vector3.up * _dropHeight) : Vector3.up * _dropHeight;
            visual.localPosition = _restPositions[slot] + up * share;
        }

        private void SettleDrop(int slot)
        {
            _dropElapsed[slot] = -1f;
            _dropCount--;

            if (_slotVisuals[slot])
            {
                _slotVisuals[slot].transform.localPosition = _restPositions[slot];
            }
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
