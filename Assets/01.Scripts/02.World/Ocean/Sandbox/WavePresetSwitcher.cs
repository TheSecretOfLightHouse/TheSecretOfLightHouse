using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class WavePresetSwitcher : MonoBehaviour
    {
        [SerializeField]
        private WaveParams[] _calm =
        {
            new WaveParams { Amplitude = 0.5f, Wavelength = 20f, Direction = new Vector2(1f, 0f), Steepness = 0.3f, Speed = 3f },
            new WaveParams { Amplitude = 0.25f, Wavelength = 9f, Direction = new Vector2(0.6f, 0.8f), Steepness = 0.2f, Speed = 2.2f }
        };

        [SerializeField]
        private WaveParams[] _storm =
        {
            new WaveParams { Amplitude = 2.5f, Wavelength = 40f, Direction = new Vector2(1f, 0f), Steepness = 0.35f, Speed = 5f },
            new WaveParams { Amplitude = 1.2f, Wavelength = 22f, Direction = new Vector2(0.7071f, 0.7071f), Steepness = 0.25f, Speed = 4f },
            new WaveParams { Amplitude = 0.5f, Wavelength = 9f, Direction = new Vector2(0.6f, 0.8f), Steepness = 0.1f, Speed = 3f }
        };

        [ContextMenu("Apply Calm")]
        public void ApplyCalm()
        {
            Apply(_calm, nameof(_calm));
        }

        [ContextMenu("Apply Storm")]
        public void ApplyStorm()
        {
            Apply(_storm, nameof(_storm));
        }

        private void Apply(WaveParams[] waves, string label)
        {
            if (!WorldServices.TryGet(out WaterSamplerHost host))
            {
                Debug.LogError($"[{nameof(WavePresetSwitcher)}] WaterSamplerHost is missing.");
                return;
            }

            if (!host.Inputs.TrySetWaves(waves))
            {
                Debug.LogError($"[{nameof(WavePresetSwitcher)}] {label} waves are invalid (direction must be unit length within 0.01, steepness sum <= {WaveModel.MaxTotalSteepness}, wavelength > 0).");
                return;
            }

            Debug.Log($"[{nameof(WavePresetSwitcher)}] Applied {label} ({waves.Length} waves)");
        }
    }
}
