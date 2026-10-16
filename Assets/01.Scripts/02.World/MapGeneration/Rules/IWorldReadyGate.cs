using System;
using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public interface IWorldReadyGate
    {
        event Action Changed;

        bool AreAllReady(IReadOnlyList<uint> roster);
    }
}
