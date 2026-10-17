using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class StormWallWaveSource : IWaveModifierSource
    {
        private readonly IReadOnlyList<StormWallField> _fields;
        private readonly WaveModifierData[] _waves;

        public int MaxCount => _waves.Length;

        public StormWallWaveSource(IReadOnlyList<StormWallField> fields,
            StormWallConfig config)
        {
            if (fields == null || fields.Count == 0
                || config == null || !config.IsValid)
            {
                throw new ArgumentException("Storm wave setup is invalid.");
            }

            _fields = fields;
            _waves = new WaveModifierData[fields.Count];

            for (int i = 0; i < fields.Count; i++)
            {
                _waves[i] = new WaveModifierData(
                    WaveModifierKind.RingWave,
                    Vector2.zero,
                    fields[i].Radius,
                    config.WaveAmplitude,
                    0f,
                    fields[i].Width,
                    config.WaveWavelength,
                    config.WaveSpeed);
            }

            if (!WaveModifierModel.AreValid(_waves))
            {
                throw new ArgumentException("Storm wave data is invalid.");
            }
        }

        public int WriteModifiers(Span<WaveModifierData> destination)
        {
            if (destination.Length < MaxCount)
            {
                return -1;
            }

            int count = 0;
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i].IsActive)
                {
                    destination[count++] = _waves[i];
                }
            }

            return count;
        }
    }
}
