using System;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IWaveModifierSource
    {
        int MaxCount { get; }

        int WriteModifiers(Span<WaveModifierData> destination);
    }
}
