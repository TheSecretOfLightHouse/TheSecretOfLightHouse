#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Workbench
{
    // JH03 scripted check in the workbench test scene. Runs with "-jh03Auto" on a build, or when an editor session
    // sets IsEnabledInThisProcess (via MCP) before the match. Actor 1 walks the rules; with two players, actor 2
    // checks the remote view, fills another recipe on its own, and both race for the last missing unit of the gear. Expects DevWorkbenchResourceSeeder's stock
    // (stone 5, wood 5, fiber 3, iron 6, glass 2, crystal 2) and the four test recipes with cap 1.
    public sealed class WorkbenchTestAutomation : MonoBehaviour
    {
        private const string AutoFlag = "-jh03Auto";
        private const float ReplyTimeoutSeconds = 10f;
        private const float StartTimeoutSeconds = 120f;
        private const float StateTimeoutSeconds = 5f;
        private const float RemoteTimeoutSeconds = 90f;

        private const int Stone = 1;
        private const int Fiber = 3;
        private const int Iron = 4;
        private const int Glass = 5;
        private const int Crystal = 6;
        private const int UnknownId = 999;

        private const int FrameRecipe = 1;
        private const int GearRecipe = 2;
        private const int LensRecipe = 3;

        private const int SeedIron = 6;
        private const int SeedGlass = 2;

        [SerializeField]
        private WorkbenchNet _workbench;

        private InteractionRelay _relay;
        private PlayerInventory _inventory;
        private bool _hasReply;
        private uint _awaitedSequence;
        private InteractionResult _reply;
        private int _passCount;
        private int _failCount;

        public static bool IsEnabledInThisProcess { get; set; }

        private void Start()
        {
            if (IsEnabledInThisProcess || Array.IndexOf(Environment.GetCommandLineArgs(), AutoFlag) >= 0)
            {
                StartCoroutine(Run());
            }
        }

        private void OnDestroy()
        {
            if (_relay)
            {
                _relay.ResultReceived -= HandleResultReceived;
            }
        }

        private IEnumerator Run()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            double deadline = Time.realtimeSinceStartupAsDouble + StartTimeoutSeconds;

            while ((!NetworkClient.localPlayer || manager.ClientState.Phase != MatchPhase.InProgress)
                && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!NetworkClient.localPlayer || !_workbench)
            {
                Log("ABORT no local player or workbench");
                yield break;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();
            _inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();
            _relay.ResultReceived += HandleResultReceived;
            uint actorId = NetworkClient.localPlayer.GetComponent<NetworkActor>().ActorId;
            Log($"START actor={actorId} host={NetworkServer.active} players={manager.ClientState.PlayerCount}");

            yield return WaitFor("seeded", () => Held(Iron) == SeedIron && Held(Glass) == SeedGlass, StateTimeoutSeconds);

            if (actorId == 1)
            {
                yield return RunRules();
                yield return RunRace(actorId, manager.ClientState.PlayerCount >= 2);
            }
            else
            {
                yield return RunRemote(actorId);
            }

            Log($"ALL_DONE actor={actorId} pass={_passCount} fail={_failCount}");
        }

        private IEnumerator RunRules()
        {
            yield return SendAndExpect("deposit_unknown_recipe", InteractionAction.Deposit, UnknownId, Iron, 1, InteractionResultCode.Rejected);
            yield return SendAndExpect("deposit_zero_recipe", InteractionAction.Deposit, 0, Iron, 1, InteractionResultCode.Rejected);

            // Lens needs 3 glass, holds 2: asks for 5, only the 2 held go in.
            yield return SendAndExpect("deposit_partial", InteractionAction.Deposit, LensRecipe, Glass, 5, InteractionResultCode.Success);
            yield return WaitFor("deposit_partial_state",
                () => Held(Glass) == 0 && _workbench.GetDeposited(LensRecipe, Glass) == 2 && _workbench.GetRemaining(LensRecipe, Glass) == 1,
                StateTimeoutSeconds);

            // Every recipe keeps its own progress (2026-10-18): starting gear leaves the lens untouched.
            yield return SendAndExpect("deposit_other_recipe", InteractionAction.Deposit, GearRecipe, Fiber, 1, InteractionResultCode.Success);
            yield return WaitFor("both_recipes_in_progress",
                () => _workbench.IsStarted(GearRecipe) && _workbench.GetDeposited(GearRecipe, Fiber) == 1
                    && _workbench.GetDeposited(LensRecipe, Glass) == 2, StateTimeoutSeconds);

            yield return SendAndExpect("deposit_none_held", InteractionAction.Deposit, LensRecipe, Glass, 1, InteractionResultCode.Rejected);
            yield return SendAndExpect("deposit_not_in_recipe", InteractionAction.Deposit, LensRecipe, Stone, 1, InteractionResultCode.Rejected);
            Check("deposit_not_in_recipe_kept", Held(Stone) == 5, $"stone={Held(Stone)}");
            yield return SendAndExpect("deposit_zero", InteractionAction.Deposit, LensRecipe, Iron, 0, InteractionResultCode.Rejected);
            yield return SendAndExpect("deposit_negative", InteractionAction.Deposit, LensRecipe, Iron, -1, InteractionResultCode.Rejected);
            yield return SendAndExpect("deposit_unknown_resource", InteractionAction.Deposit, LensRecipe, UnknownId, 1, InteractionResultCode.Rejected);
            Check("rejects_kept_iron", Held(Iron) == SeedIron, $"iron={Held(Iron)}");

            // The host can top up on the server directly; a remote client has no such path.
            if (NetworkServer.active)
            {
                _inventory.TryAdd(Glass, 3);
                yield return WaitFor("granted_glass", () => Held(Glass) == 3, StateTimeoutSeconds);
                yield return SendAndExpect("deposit_capped", InteractionAction.Deposit, LensRecipe, Glass, 3, InteractionResultCode.Success);
                yield return WaitFor("deposit_capped_state",
                    () => Held(Glass) == 2 && _workbench.GetDeposited(LensRecipe, Glass) == 3 && _workbench.GetRemaining(LensRecipe, Glass) == 0,
                    StateTimeoutSeconds);
                yield return SendAndExpect("deposit_when_full", InteractionAction.Deposit, LensRecipe, Glass, 1, InteractionResultCode.Rejected);
            }
            else
            {
                Log("SKIP deposit_capped (not host)");
            }

            yield return SendAndExpect("craft_not_full", InteractionAction.Craft, LensRecipe, 0, 0, InteractionResultCode.Rejected);
            yield return SendAndExpect("craft_unknown_recipe", InteractionAction.Craft, UnknownId, 0, 0, InteractionResultCode.Rejected);
            yield return SendAndExpect("deposit_iron", InteractionAction.Deposit, LensRecipe, Iron, 2, InteractionResultCode.Success);
            yield return SendAndExpect("deposit_crystal", InteractionAction.Deposit, LensRecipe, Crystal, 2, InteractionResultCode.Success);

            if (NetworkServer.active)
            {
                // A full recipe stays on the bench until craft is pressed.
                yield return WaitFor("lens_waits_for_craft",
                    () => _workbench.IsReadyToCraft(LensRecipe) && !_workbench.IsReadyToCraft(GearRecipe)
                        && _workbench.GetStoredCount(LensRecipe) == 0, StateTimeoutSeconds);
                yield return SendAndExpect("craft_lens", InteractionAction.Craft, LensRecipe, 0, 0, InteractionResultCode.Success);
                yield return WaitFor("lens_completed",
                    () => !_workbench.IsStarted(LensRecipe) && _workbench.GetStoredCount(LensRecipe) == 1
                        && Held(Iron) == SeedIron - 2 && Held(Crystal) == 0, StateTimeoutSeconds);
                Check("gear_kept_after_lens", _workbench.GetDeposited(GearRecipe, Fiber) == 1, Describe());
                // Cap 1 and the lens exists, so a new lens cannot start.
                yield return SendAndExpect("deposit_capped_recipe", InteractionAction.Deposit, LensRecipe, Iron, 1, InteractionResultCode.Rejected);
            }
        }

        // Gear needs iron 3 + fiber 3. Actor 1 leaves it one iron short; with two players both then send that last unit.
        private IEnumerator RunRace(uint actorId, bool isRacing)
        {
            yield return SendAndExpect("deposit_fiber", InteractionAction.Deposit, GearRecipe, Fiber, 3, InteractionResultCode.Success);
            yield return SendAndExpect("deposit_iron_short", InteractionAction.Deposit, GearRecipe, Iron, 2, InteractionResultCode.Success);
            yield return WaitFor("gear_one_short", () => _workbench.GetRemaining(GearRecipe, Iron) == 1, StateTimeoutSeconds);

            if (isRacing)
            {
                // Give actor 2 time to see the same state, then send at about the same moment.
                yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return SendLastUnit(actorId);
        }

        private IEnumerator RunRemote(uint actorId)
        {
            yield return WaitFor("remote_sees_stored_lens", () => _workbench.GetStoredCount(LensRecipe) == 1, RemoteTimeoutSeconds);
            yield return WaitFor("remote_sees_gear_short", () => _workbench.GetRemaining(GearRecipe, Iron) == 1, RemoteTimeoutSeconds);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return SendLastUnit(actorId);

            // A remote player fills a different recipe on its own.
            yield return SendAndExpect("remote_deposit_frame", InteractionAction.Deposit, FrameRecipe, Stone, 2, InteractionResultCode.Success);
            yield return WaitFor("remote_frame_state", () => _workbench.GetDeposited(FrameRecipe, Stone) == 2 && Held(Stone) == 3, StateTimeoutSeconds);
            yield return SendAndExpect("remote_deposit_not_in_recipe", InteractionAction.Deposit, FrameRecipe, Glass, 1, InteractionResultCode.Rejected);
        }

        // Either result is allowed; what must hold is that the inventory moved exactly when the reply says Success,
        // and that the gear finished once. The two actors' logs together must show at most one RACE Success and
        // exactly one CRAFT_RACE Success.
        private IEnumerator SendLastUnit(uint actorId)
        {
            int ironBefore = Held(Iron);
            yield return Send("race_last_unit", InteractionAction.Deposit, GearRecipe, Iron, 1);
            bool isSuccess = _hasReply && _reply.IsSuccess;
            Log($"RACE actor={actorId} code={(_hasReply ? _reply.Code.ToString() : "none")}");
            int expectedIron = isSuccess ? ironBefore - 1 : ironBefore;
            yield return WaitFor("race_inventory_matches", () => Held(Iron) == expectedIron, StateTimeoutSeconds);
            yield return WaitFor("gear_ready",
                () => _workbench.IsReadyToCraft(GearRecipe) || _workbench.GetStoredCount(GearRecipe) == 1, StateTimeoutSeconds);

            // Both actors press craft at about the same moment; the two logs together must show one Success.
            yield return Send("craft_race", InteractionAction.Craft, GearRecipe, 0, 0);
            Log($"CRAFT_RACE actor={actorId} code={(_hasReply ? _reply.Code.ToString() : "none")}");
            yield return WaitFor("gear_completed_once",
                () => !_workbench.IsStarted(GearRecipe) && _workbench.GetStoredCount(GearRecipe) == 1, StateTimeoutSeconds);
        }

        private IEnumerator SendAndExpect(string step, InteractionAction action, int arg0, int arg1, int arg2, InteractionResultCode expected)
        {
            yield return Send(step, action, arg0, arg1, arg2);

            if (_hasReply)
            {
                Check(step, _reply.Code == expected, $"code={_reply.Code} expected={expected} seq={_reply.Sequence}");
            }
        }

        private IEnumerator Send(string step, InteractionAction action, int arg0, int arg1, int arg2)
        {
            _hasReply = false;
            _awaitedSequence = _relay.NextSequenceForTest;

            if (!_relay.TrySendToNetworkTarget(_workbench.netId, action, arg0, arg1, arg2))
            {
                Check(step, false, "send refused");
                yield break;
            }

            double deadline = Time.realtimeSinceStartupAsDouble + ReplyTimeoutSeconds;

            while (!_hasReply && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!_hasReply)
            {
                Check(step, false, "no reply");
            }
        }

        // Shared state reaches a remote client after the reply, so state checks poll until they hold or time out.
        private IEnumerator WaitFor(string step, Func<bool> condition, float timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;

            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Check(step, condition(), Describe());
        }

        private int Held(int resourceId)
        {
            return _inventory ? _inventory.GetSyncedCount(resourceId) : 0;
        }

        private string Describe()
        {
            return $"lens(glass={_workbench.GetDeposited(LensRecipe, Glass)} iron={_workbench.GetDeposited(LensRecipe, Iron)}) " +
                $"gear(iron={_workbench.GetDeposited(GearRecipe, Iron)} fiber={_workbench.GetDeposited(GearRecipe, Fiber)}) " +
                $"storedLens={_workbench.GetStoredCount(LensRecipe)} storedGear={_workbench.GetStoredCount(GearRecipe)} " +
                $"held(iron={Held(Iron)} glass={Held(Glass)} crystal={Held(Crystal)} fiber={Held(Fiber)})";
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

        private static void Log(string message)
        {
            Debug.Log($"[{nameof(WorkbenchTestAutomation)}] {message}");
        }
    }
}
#endif
