using System;
using System.Collections.Generic;
using Lighthouse.Network.Connections.Server;
using Lighthouse.Network.Session.Rules;

namespace Lighthouse.Network.Session.Server
{
    // Server-only roles of the current match. The manager fills it once at the start, before MatchStarted,
    // so every feature reads the same mafia. Keyed by ActorId: player objects and their netIds are recreated per scene.
    public sealed class MatchRoles
    {
        private readonly HashSet<uint> _participants = new HashSet<uint>();
        private uint _mafiaActorId = ConnectionDirectory.InvalidActorId;

        public bool IsAssigned => _mafiaActorId != ConnectionDirectory.InvalidActorId;
        // InvalidActorId until assigned.
        public uint MafiaActorId => _mafiaActorId;

        // One match per server run, so roles are given once and never reshuffled.
        public void Assign(IReadOnlyList<uint> roster, int mafiaIndex)
        {
            if (IsAssigned)
            {
                throw new InvalidOperationException("Roles are already assigned for this match.");
            }

            if (roster == null || mafiaIndex < 0 || mafiaIndex >= roster.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(mafiaIndex), "The mafia must be one of the roster.");
            }

            for (int i = 0; i < roster.Count; i++)
            {
                _participants.Add(roster[i]);
            }

            _mafiaActorId = roster[mafiaIndex];
        }

        public bool IsMafia(uint actorId)
        {
            return IsAssigned && actorId == _mafiaActorId;
        }

        public MatchRole GetRole(uint actorId)
        {
            if (!IsAssigned || !_participants.Contains(actorId))
            {
                return MatchRole.None;
            }

            return actorId == _mafiaActorId ? MatchRole.Mafia : MatchRole.Citizen;
        }

        public void Clear()
        {
            _participants.Clear();
            _mafiaActorId = ConnectionDirectory.InvalidActorId;
        }
    }
}
