using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class OceanShaderBridge : MonoBehaviour
    {
        private static readonly int WaveAId = Shader.PropertyToID("_OceanWaveA");
        private static readonly int WaveBId = Shader.PropertyToID("_OceanWaveB");
        private static readonly int WaveCountId = Shader.PropertyToID("_OceanWaveCount");
        private static readonly int TimeId = Shader.PropertyToID("_OceanTime");
        private static readonly int ModifierAId = Shader.PropertyToID("_OceanModifierA");
        private static readonly int ModifierBId = Shader.PropertyToID("_OceanModifierB");
        private static readonly int ModifierCId = Shader.PropertyToID("_OceanModifierC");
        private static readonly int ModifierCountId = Shader.PropertyToID("_OceanModifierCount");

        private static OceanShaderBridge _active;

        [SerializeField] private WaterSamplerHost _host;

        private readonly Vector4[] _waveA = new Vector4[WaveModel.MaxWaves];
        private readonly Vector4[] _waveB = new Vector4[WaveModel.MaxWaves];
        private readonly Vector4[] _modifierA = new Vector4[WaveModifierModel.MaxModifiers];
        private readonly Vector4[] _modifierB = new Vector4[WaveModifierModel.MaxModifiers];
        private readonly Vector4[] _modifierC = new Vector4[WaveModifierModel.MaxModifiers];

        private IClock _clock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwner()
        {
            _active = null;
        }

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                Debug.LogError($"[{nameof(OceanShaderBridge)}] Another shader bridge is active.");
                enabled = false;
                return;
            }

            _active = this;
        }

        private void Start()
        {
            if (_host == null || !WorldServices.TryGet<IClock>(out _clock))
            {
                Debug.LogError($"[{nameof(OceanShaderBridge)}] Water sampler and clock are required.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_host == null || _clock == null || _active != this)
            {
                return;
            }

            _host.RefreshModifiers();

            var waves = _host.Inputs.Waves;
            for (int i = 0; i < waves.Length; i++)
            {
                WaveParams wave = waves[i];
                _waveA[i] = new Vector4(
                    wave.Direction.x, wave.Direction.y, wave.Wavelength, wave.Speed);
                _waveB[i] = new Vector4(wave.Amplitude, wave.Steepness, 0f, 0f);
            }

            var modifiers = _host.Inputs.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                WaveModifierData modifier = modifiers[i];
                _modifierA[i] = new Vector4(
                    modifier.Center.x, modifier.Center.y, modifier.Radius, modifier.Strength);
                _modifierB[i] = new Vector4(
                    (int)modifier.Kind, modifier.StartTime, modifier.BandWidth, modifier.Wavelength);
                _modifierC[i] = new Vector4(modifier.Speed, 0f, 0f, 0f);
            }

            Shader.SetGlobalVectorArray(WaveAId, _waveA);
            Shader.SetGlobalVectorArray(WaveBId, _waveB);
            Shader.SetGlobalVectorArray(ModifierAId, _modifierA);
            Shader.SetGlobalVectorArray(ModifierBId, _modifierB);
            Shader.SetGlobalVectorArray(ModifierCId, _modifierC);
            Shader.SetGlobalFloat(WaveCountId, waves.Length);
            Shader.SetGlobalFloat(ModifierCountId, modifiers.Length);
            Shader.SetGlobalFloat(TimeId, (float)_clock.Now);
        }

        private void OnDisable()
        {
            if (_active != this)
            {
                return;
            }

            Shader.SetGlobalFloat(WaveCountId, 0f);
            Shader.SetGlobalFloat(ModifierCountId, 0f);
            _active = null;
        }
    }
}
