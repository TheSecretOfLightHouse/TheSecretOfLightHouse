#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Relay.View;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Interaction
{
    // JH02 A-1 scripted check on the local test player. Runs with "-jh02Auto" on a build, or when an editor session
    // sets IsEnabledInThisProcess (via MCP) before the match. Each step logs PASS/FAIL with the expected result code.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionSelector))]
    public sealed class InteractionTestAutomation : NetworkBehaviour
    {
        private const string AutoFlag = "-jh02Auto";
        private const string NearLabel = "Near";
        private const string FarLabel = "Far";
        private const uint MissingNetId = 999999;
        private const int UnknownSpotKey = 7;
        private const float ReplyTimeoutSeconds = 10f;
        private const float StartTimeoutSeconds = 120f;

        private InteractionSelector _selector;
        private InteractionRelay _relay;
        private bool _hasReply;
        private uint _awaitedSequence;
        private InteractionResult _reply;
        private int _passCount;
        private int _failCount;

        public static bool IsEnabledInThisProcess { get; set; }

        private void Awake()
        {
            _selector = GetComponent<InteractionSelector>();
            _relay = GetComponent<InteractionRelay>();
        }

        public override void OnStartLocalPlayer()
        {
            if (IsEnabledInThisProcess || Array.IndexOf(Environment.GetCommandLineArgs(), AutoFlag) >= 0)
            {
                _relay.ResultReceived += HandleResultReceived;
                StartCoroutine(Run());
            }
        }

        public override void OnStopLocalPlayer()
        {
            _relay.ResultReceived -= HandleResultReceived;
        }

        private IEnumerator Run()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            uint actorId = GetComponent<NetworkActor>().ActorId;
            Log($"START actor={actorId} host={NetworkServer.active} phase={manager.ClientState.Phase}");

            // Before everyone loaded. Only the host can be sure the server is still Loading when its request arrives.
            if (NetworkServer.active && manager.Phase == MatchPhase.Loading)
            {
                yield return SendAndExpect("early_loading", () => _relay.TrySendToNetworkTarget(FindNetId(NearLabel), InteractionAction.PickUp),
                    InteractionResultCode.NotInProgress);
            }
            else
            {
                Log("SKIP early_loading (not host or already in progress)");
            }

            double deadline = Time.realtimeSinceStartupAsDouble + StartTimeoutSeconds;

            while (manager.ClientState.Phase != MatchPhase.InProgress && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            uint nearId = FindNetId(NearLabel);
            uint farId = FindNetId(FarLabel);

            yield return SendAndExpect("normal", () => _relay.TrySendToNetworkTarget(nearId, InteractionAction.PickUp),
                InteractionResultCode.Success);
            uint firstSequence = _reply.Sequence;

            yield return SendUncheckedAndExpect("replay_last",
                InteractionRequest.ForNetworkTarget(firstSequence, nearId, InteractionAction.PickUp, 0, 0), InteractionResultCode.Success);
            yield return SendUncheckedAndExpect("sequence_gap",
                InteractionRequest.ForNetworkTarget(_relay.NextSequenceForTest + 5, nearId, InteractionAction.PickUp, 0, 0),
                InteractionResultCode.SequenceGap);
            yield return SendAndExpect("out_of_range", () => _relay.TrySendToNetworkTarget(farId, InteractionAction.PickUp),
                InteractionResultCode.OutOfRange);
            yield return SendAndExpect("unsupported_action", () => _relay.TrySendToNetworkTarget(nearId, InteractionAction.Deposit),
                InteractionResultCode.ActionNotSupported);
            yield return SendAndExpect("missing_target", () => _relay.TrySendToNetworkTarget(MissingNetId, InteractionAction.PickUp),
                InteractionResultCode.InvalidTarget);
            yield return SendAndExpect("no_handler", () => _relay.TrySendToNetworkTarget(nearId, InteractionAction.Withdraw),
                InteractionResultCode.NotHandled);
            yield return SendAndExpect("non_network_no_adapter",
                () => _relay.TrySendToNonNetworkTarget(InteractionTargetKind.HarvestSpot, UnknownSpotKey, InteractionAction.Harvest),
                InteractionResultCode.InvalidTarget);
            yield return SendUncheckedAndExpect("stale_sequence",
                InteractionRequest.ForNetworkTarget(firstSequence, nearId, InteractionAction.PickUp, 0, 0), InteractionResultCode.StaleSequence);

            // Selector path: the near target must be the one picked around the spawn point.
            InteractionTarget selected = _selector.Selected;
            Check("selector_picks_near", selected && selected.NetId == nearId, selected ? selected.name : "none");
            yield return SendAndExpect("selector_interact", () => _selector.TryInteract(), InteractionResultCode.Success);

            // One request at a time: a second send before the reply is refused on the client.
            bool isFirstSent = _relay.TrySendToNetworkTarget(nearId, InteractionAction.Deposit);
            bool isSecondSent = _relay.TrySendToNetworkTarget(nearId, InteractionAction.Deposit);
            Check("busy_guard", isFirstSent && !isSecondSent, $"first={isFirstSent} second={isSecondSent}");

            while (_relay.IsAwaitingResult)
            {
                yield return null;
            }

            Log($"ALL_DONE actor={actorId} pass={_passCount} fail={_failCount}");
        }

        private IEnumerator SendAndExpect(string step, Func<bool> send, InteractionResultCode expected)
        {
            uint sequence = _relay.NextSequenceForTest;
            _hasReply = false;
            _awaitedSequence = sequence;

            if (!send())
            {
                Check(step, false, "send refused");
                yield break;
            }

            yield return WaitForReply(step, expected);
        }

        private IEnumerator SendUncheckedAndExpect(string step, InteractionRequest request, InteractionResultCode expected)
        {
            _hasReply = false;
            _awaitedSequence = request.Sequence;
            _relay.SendUncheckedForTest(request);
            yield return WaitForReply(step, expected);
        }

        private IEnumerator WaitForReply(string step, InteractionResultCode expected)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + ReplyTimeoutSeconds;

            while (!_hasReply && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!_hasReply)
            {
                Check(step, false, "no reply");
                yield break;
            }

            Check(step, _reply.Code == expected,
                $"code={_reply.Code} expected={expected} seq={_reply.Sequence} next={_reply.NextExpectedSequence}");
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (result.Sequence == _awaitedSequence)
            {
                _reply = result;
                _hasReply = true;
            }
        }

        private void Check(string step, bool isPassed, string detail)
        {
            if (isPassed)
            {
                _passCount++;
            }
            else
            {
                _failCount++;
            }

            Log($"{(isPassed ? "PASS" : "FAIL")} step={step} {detail}");
        }

        private static uint FindNetId(string label)
        {
            DevInteractionCounter counter = DevInteractionCounter.FindByLabel(label);
            return counter ? counter.netId : 0;
        }

        private static void Log(string message)
        {
            Debug.Log($"[{nameof(InteractionTestAutomation)}] {message}");
        }
    }
}
#endif
