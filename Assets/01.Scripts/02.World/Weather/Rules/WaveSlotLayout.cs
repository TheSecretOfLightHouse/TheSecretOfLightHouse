using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    [CreateAssetMenu(fileName = "WaveSlotLayout", menuName = "TheLightHouse/World/WaveSlotLayout")]
    public class WaveSlotLayout : ScriptableObject
    {
        private const float MinWavelength = 0.1f;
        private const float MinDirectionSqrMagnitude = 1e-4f;

        [SerializeField]
        private WaveSlot[] _slots =
        {
            new WaveSlot { Wavelength = 40f, Direction = new Vector2(1f, 0f), Speed = 5f },
            new WaveSlot { Wavelength = 22f, Direction = new Vector2(0.7071f, 0.7071f), Speed = 4f },
            new WaveSlot { Wavelength = 12f, Direction = new Vector2(0.6f, 0.8f), Speed = 3f },
            new WaveSlot { Wavelength = 6f, Direction = new Vector2(-0.6f, 0.8f), Speed = 2.5f }
        };

        public int Count => _slots != null ? _slots.Length : 0;

        public WaveSlot GetSlot(int index)
        {
            WaveSlot slot = _slots[index];
            slot.Direction = slot.Direction.sqrMagnitude < MinDirectionSqrMagnitude
                ? Vector2.right
                : slot.Direction.normalized;
            return slot;
        }

        private void OnValidate()
        {
            if (_slots == null)
            {
                return;
            }

            if (_slots.Length > WaveModel.MaxWaves)
            {
                Array.Resize(ref _slots, WaveModel.MaxWaves);
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Wavelength = Mathf.Max(MinWavelength, _slots[i].Wavelength);
            }
        }
    }
}
