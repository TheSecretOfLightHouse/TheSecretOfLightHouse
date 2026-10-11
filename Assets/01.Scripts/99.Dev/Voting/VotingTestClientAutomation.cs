using System;
using System.Collections;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Voting
{
    public sealed class VotingTestClientAutomation : MonoBehaviour
    {
        [SerializeField] private VotingTestPanel _panel;
        [SerializeField] private VotingNetworkController _voting;

        private bool _isAutomated;
        private bool _hasResponse;
        private string _step;
        private VoteChangeResult _expected;

        private IEnumerator Start()
        {
            _isAutomated = Array.IndexOf(Environment.GetCommandLineArgs(), "--voting-auto-client") >= 0;
            if (!_isAutomated)
            {
                yield break;
            }

            _voting.VoteResponded += HandleVoteResponded;
            _panel.StartClient("localhost");
            yield return WaitForState(false, false);
            VotingTestPlayer player = VotingTestPlayer.LocalPlayer;
            if (player == null || !_voting.HasOwnVoteState)
            {
                throw new InvalidOperationException("The automated client did not receive its initial state.");
            }

            uint ownId = player.ActorId;
            bool isMafia = _voting.OwnVoteState.RemainingVoteCount == 0;
            uint firstTarget = ownId == 2 ? 3u : 2u;
            uint secondTarget = ownId == 2 ? 4u : 1u;
            yield return RequestAndWait(firstTarget, "first", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.Changed);
            yield return RequestAndWait(firstTarget, "repeat", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.Unchanged);
            yield return RequestAndWait(ownId, "self", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.CannotVoteForSelf);
            yield return RequestAndWait(0, "withdraw", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.UnknownTarget);
            yield return RequestAndWait(uint.MaxValue, "unknown", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.UnknownTarget);
            yield return RequestAndWait(secondTarget, "second", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.Changed);
            yield return RequestAndWait(firstTarget, "third", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.NoVotesRemaining);
            Debug.Log($"[{nameof(VotingTestClientAutomation)}] BASIC_DONE actor={ownId}");

            yield return WaitForState(true, false);
            yield return RequestAndWait(firstTarget, "locked_change", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.VotingLocked);
            yield return RequestAndWait(secondTarget, "locked_repeat", isMafia ? VoteChangeResult.MafiaCannotVote : VoteChangeResult.Unchanged);
            Debug.Log($"[{nameof(VotingTestClientAutomation)}] LOCK_DONE actor={ownId}");

            yield return WaitForState(true, true);
            yield return RequestAndWait(secondTarget, "ended", VoteChangeResult.GameEnded);
            Debug.Log($"[{nameof(VotingTestClientAutomation)}] ALL_DONE actor={ownId}");
        }

        private void OnDestroy()
        {
            if (_voting != null)
            {
                _voting.VoteResponded -= HandleVoteResponded;
            }
        }

        private IEnumerator WaitForState(bool requiresLock, bool requiresEnd)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 120;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (_voting.HasOwnVoteState && (!requiresLock || _voting.OwnVoteState.IsLocked)
                    && (!requiresEnd || _voting.OwnVoteState.IsEnded))
                {
                    yield break;
                }
                yield return null;
            }
            throw new TimeoutException("Timed out waiting for the host's test state.");
        }

        private IEnumerator RequestAndWait(uint targetActorId, string step, VoteChangeResult expected)
        {
            _step = step;
            _expected = expected;
            _hasResponse = false;
            _voting.RequestVote(targetActorId);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!_hasResponse && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }
            if (!_hasResponse)
            {
                throw new TimeoutException("No personal response for " + step);
            }
        }

        private void HandleVoteResponded(VoteResponse response)
        {
            if (!_isAutomated || _step == null || response.Result == VoteChangeResult.StateRetrieved)
            {
                return;
            }
            bool isPassed = response.Result == _expected;
            uint ownId = VotingTestPlayer.LocalPlayer != null ? VotingTestPlayer.LocalPlayer.ActorId : 0;
            Debug.Log($"[{nameof(VotingTestClientAutomation)}] {(isPassed ? "PASS" : "FAIL")} actor={ownId} step={_step} result={response.Result} target={response.TargetActorId} remaining={response.RemainingVoteCount}");
            if (!isPassed)
            {
                throw new InvalidOperationException("Unexpected personal response at " + _step);
            }
            _hasResponse = true;
        }
    }
}
