#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Network.Session.Net;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // JH07 test aid: stands in for MK06 until role assignment exists. Picks a random mafia only for tests;
    // the real caller of InitializeVoting and the assignment rule are MK06 (최명기) and TW03 (박태욱) decisions.
    [DisallowMultipleComponent]
    public sealed class MatchStartVotingProbe : MonoBehaviour
    {
        [SerializeField]
        private VotingNetworkController _voting;

        private LighthouseNetworkManager _manager;

        private void OnEnable()
        {
            _manager = LighthouseNetworkManager.Instance;

            if (!_manager || !_voting)
            {
                Debug.LogError($"[{nameof(MatchStartVotingProbe)}] Network manager and voting controller are required.", this);
                return;
            }

            _manager.MatchStarted += HandleMatchStarted;
        }

        private void OnDisable()
        {
            if (_manager)
            {
                _manager.MatchStarted -= HandleMatchStarted;
            }
        }

        private void HandleMatchStarted(uint matchId, double startTime, IReadOnlyList<uint> roster)
        {
            uint mafiaActorId = roster[Random.Range(0, roster.Count)];

            _voting.InitializeVoting(roster, mafiaActorId);
            Debug.Log($"[{nameof(MatchStartVotingProbe)}] Match {matchId} voting initialized. players={roster.Count} mafia={mafiaActorId}");
        }
    }
}
#endif
