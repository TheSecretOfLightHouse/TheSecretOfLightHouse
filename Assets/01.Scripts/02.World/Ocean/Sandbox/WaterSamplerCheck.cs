using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class WaterSamplerCheck : MonoBehaviour
    {
        private const float ModifierRadius = 8f;
        private const float ModifierStrength = 1f;

        [SerializeField]
        private WaveParams[] _waves =
        {
            new WaveParams { Amplitude = 0.5f, Wavelength = 20f, Direction = new Vector2(1f, 0f), Steepness = 0.3f, Speed = 3f },
            new WaveParams { Amplitude = 0.25f, Wavelength = 9f, Direction = new Vector2(0.6f, 0.8f), Steepness = 0.2f, Speed = 2.2f }
        };

        [SerializeField] private float _time = 1.5f;
        [SerializeField] private bool _isModifierEnabled = true;

        [ContextMenu("Sampler/Run All Tests")]
        private void RunAllTests()
        {
            WaveModifierData[] modifiers = BuildModifiers();

            bool isAllPassed = SamplerSlotChecks.RunAll();
            isAllPassed &= WaterSamplerChecks.RunAll(_waves, modifiers, _time);

            Debug.Log($"[{nameof(WaterSamplerCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }

        private WaveModifierData[] BuildModifiers()
        {
            if (!_isModifierEnabled)
            {
                return Array.Empty<WaveModifierData>();
            }

            return new[] { new WaveModifierData(WaveModifierKind.Bump, Vector2.zero, ModifierRadius, ModifierStrength, 0f) };
        }
    }
}
