using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BreakerSwell
    {
        public static bool TryGet(ReadOnlySpan<WaveParams> waves, out Vector2 direction, out float energy)
        {
            direction = Vector2.zero;
            energy = 0f;
            float strongest = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                float amplitude = waves[i].Amplitude;
                energy += amplitude;
                if (amplitude > strongest && waves[i].Direction.sqrMagnitude > 0f)
                {
                    strongest = amplitude;
                    direction = waves[i].Direction.normalized;
                }
            }

            return strongest > 0f;
        }
    }
}
