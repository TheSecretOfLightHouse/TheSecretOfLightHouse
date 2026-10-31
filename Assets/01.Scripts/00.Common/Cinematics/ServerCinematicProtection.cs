using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Lighthouse.Common.Cinematics
{
    public static class ServerCinematicProtection
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();
        private static double _startedAt;
        private static double _pausedSeconds;

        /// <summary>Whether summon presentation currently blocks server damage and recovery.</summary>
        public static bool IsActive => NetworkServer.active && Owners.Count > 0;

        // Recovery deadlines use this clock; world and transport clocks keep advancing.
        /// <summary>Network time excluding presentation windows, for recovery deadlines only.</summary>
        public static double RecoveryTime => (Owners.Count > 0 ? _startedAt : NetworkTime.time) - _pausedSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Owners.Clear();
            _startedAt = 0d;
            _pausedSeconds = 0d;
        }

        /// <summary>Acquires protection without changing any other invulnerability source.</summary>
        public static void Begin(object owner)
        {
            if (!NetworkServer.active || owner == null || !Owners.Add(owner))
            {
                return;
            }
            if (Owners.Count == 1)
            {
                _startedAt = NetworkTime.time;
            }
        }

        /// <summary>Releases this owner's protection and resumes recovery when the last owner leaves.</summary>
        public static void End(object owner)
        {
            if (owner == null || !Owners.Remove(owner) || Owners.Count > 0)
            {
                return;
            }
            _pausedSeconds += System.Math.Max(0d, NetworkTime.time - _startedAt);
            _startedAt = 0d;
        }
    }
}
