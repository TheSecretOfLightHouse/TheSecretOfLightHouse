using System;

namespace Lighthouse.World.Ocean.Rules
{
    public class WaveInputs
    {
        private readonly WaveParams[] _waves = new WaveParams[WaveModel.MaxWaves];
        private readonly WaveModifierData[] _modifiers = new WaveModifierData[WaveModifierModel.MaxModifiers];
        private int _waveCount;
        private int _modifierCount;

        public ReadOnlySpan<WaveParams> Waves => new ReadOnlySpan<WaveParams>(_waves, 0, _waveCount);

        public ReadOnlySpan<WaveModifierData> Modifiers => new ReadOnlySpan<WaveModifierData>(_modifiers, 0, _modifierCount);

        public bool TrySetWaves(ReadOnlySpan<WaveParams> waves)
        {
            if (!WaveModel.IsValid(waves))
            {
                return false;
            }

            waves.CopyTo(_waves);
            _waveCount = waves.Length;
            return true;
        }

        public bool TrySetModifiers(ReadOnlySpan<WaveModifierData> modifiers)
        {
            if (!WaveModifierModel.AreValid(modifiers))
            {
                return false;
            }

            modifiers.CopyTo(_modifiers);
            _modifierCount = modifiers.Length;
            return true;
        }

        public void ClearModifiers()
        {
            _modifierCount = 0;
        }
    }
}
