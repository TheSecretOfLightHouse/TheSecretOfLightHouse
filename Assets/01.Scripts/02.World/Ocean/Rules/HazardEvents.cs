using System;

namespace Lighthouse.World.Ocean.Rules
{
    public class HazardEvents : IHazardEvents
    {
        public event Action<HazardHit> HazardOccurred;

        public void Raise(in HazardHit hit)
        {
            HazardOccurred?.Invoke(hit);
        }
    }
}
