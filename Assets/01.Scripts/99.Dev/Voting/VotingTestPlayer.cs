#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.Network.Connections.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Voting
{
    [RequireComponent(typeof(NetworkActor))]
    public sealed class VotingTestPlayer : NetworkBehaviour
    {
        private static readonly List<VotingTestPlayer> Players = new();

        private NetworkActor _actor;

        public static IReadOnlyList<VotingTestPlayer> VisiblePlayers => Players;
        public static VotingTestPlayer LocalPlayer { get; private set; }
        public uint ActorId => _actor != null ? _actor.ActorId : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayers()
        {
            Players.Clear();
            LocalPlayer = null;
        }

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        public override void OnStartClient()
        {
            Players.Add(this);
        }

        public override void OnStartLocalPlayer()
        {
            LocalPlayer = this;
        }

        public override void OnStopClient()
        {
            Players.Remove(this);
            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }
        }
    }
}
#endif
