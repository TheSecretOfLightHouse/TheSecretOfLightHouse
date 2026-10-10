using System;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IHazardEvents
    {
        event Action<HazardHit> HazardOccurred;
        void Raise(in HazardHit hit);
    }
}
