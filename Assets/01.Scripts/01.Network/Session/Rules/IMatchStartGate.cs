using System;
using System.Collections.Generic;

namespace Lighthouse.Network.Session.Rules
{
    // Server-only. A feature that must finish its own setup before the match starts (e.g. World map ready)
    // registers one on LighthouseNetworkManager. Lives here so the session code never depends on feature code.
    public interface IMatchStartGate
    {
        // Raise whenever AreAllReady may have changed; the manager re-checks the start on it.
        event Action Changed;

        bool AreAllReady(IReadOnlyList<uint> roster);
    }
}
