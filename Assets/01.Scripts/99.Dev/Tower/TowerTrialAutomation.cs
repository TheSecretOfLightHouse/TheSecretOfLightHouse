#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Mafia.IdentityCheck.Net;
using Lighthouse.Mafia.IdentityCheck.Rules;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Mafia.Voting.Rules;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Tower
{
    // JH04 scripted match in the trial scene. Runs with "-jh04Auto" on a build, or when an editor session sets
    // IsEnabledInThisProcess (via MCP). The host plays the mafia and starts the round; every client is a citizen
    // and points at the host, assumed to hold the lowest actor id (the host registers first).
    //   ritual: host fills the tower, checks identity without summoning, starts the ritual -> citizens win.
    //   final:  host installs one floor (25%), checks and summons -> repair kept, health at the fixed maximum;
    //           the revealed mafia cannot take modules; the first citizen builds the other three floors -> citizens win.
    // One player alone (no voting, the panel's forced summon):
    //   solo_final: citizen alone builds one floor, forces the summon -> repair kept, fixed health; builds the rest -> citizens win.
    //   solo_full:  citizen alone builds all four floors and forces the summon -> citizens win at once, no final battle.
    //   solo_mafia: mafia alone builds one floor, leaves a frame in the bench, forces the summon -> cannot take it out.
    // Scenario: "-jh04Scenario=final" (default ritual). Players expected: "-jh04Players=3" (1 for the solo ones).
    public sealed class TowerTrialAutomation : MonoBehaviour
    {
        private const string AutoFlag = "-jh04Auto";
        private const string ScenarioFlag = "-jh04Scenario=";
        private const string PlayersFlag = "-jh04Players=";
        private const float ReplyTimeoutSeconds = 10f;
        private const float StartTimeoutSeconds = 180f;
        private const float StateTimeoutSeconds = 10f;
        private const float PhaseTimeoutSeconds = 240f;
        // The owner reports its position; the server sees a teleport after the interpolation delay.
        private const float MoveSettleSeconds = 0.8f;

        private const int Stone = 1;
        private const int FrameRecipe = 1;
        private const int GearRecipe = 2;
        private const int LensRecipe = 3;
        private const int CoreRecipe = 4;

        [SerializeField]
        private TowerTrialRound _round;
        [SerializeField]
        private TowerMatchNet _match;
        [SerializeField]
        private VotingNetworkController _voting;
        [SerializeField]
        private WorkbenchNet _workbench;
        [SerializeField]
        private DevResourceGrant _grant;

        private readonly List<uint> _actors = new List<uint>();
        private InteractionRelay _relay;
        private PlayerInventory _inventory;
        private bool _hasReply;
        private uint _awaitedSequence;
        private InteractionResult _reply;
        private bool _hasIdentityDecision;
        private IdentityActionResult _identityDecision;
        private bool _hasVoteReply;
        private VoteChangeResult _voteReply;
        private int _passCount;
        private int _failCount;
        private bool _hasCheckedReturn;

        public static bool IsEnabledInThisProcess { get; set; }
        public static string ScenarioInThisProcess { get; set; }
        public static int PlayersInThisProcess { get; set; }

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

            if (_voting)
            {
                _voting.IdentityResponded -= HandleIdentityResponded;
                _voting.VoteResponded -= HandleVoteResponded;
            }
        }

        private IEnumerator Run()
        {
            string scenario = ScenarioInThisProcess ?? ReadArg(ScenarioFlag, "ritual");
            int players = PlayersInThisProcess > 0 ? PlayersInThisProcess : int.Parse(ReadArg(PlayersFlag, "3"));
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            double deadline = Time.realtimeSinceStartupAsDouble + StartTimeoutSeconds;

            while ((!NetworkClient.localPlayer || manager.ClientState.Phase != MatchPhase.InProgress)
                && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!NetworkClient.localPlayer)
            {
                Log("ABORT no local player");
                yield break;
            }

            _relay = NetworkClient.localPlayer.GetComponent<InteractionRelay>();
            _inventory = NetworkClient.localPlayer.GetComponent<PlayerInventory>();
            _relay.ResultReceived += HandleResultReceived;
            _voting.IdentityResponded += HandleIdentityResponded;
            _voting.VoteResponded += HandleVoteResponded;
            uint actorId = NetworkClient.localPlayer.GetComponent<NetworkActor>().ActorId;
            yield return WaitFor("all_players_spawned", () => RefreshActors() >= players, 30f);
            Log($"START actor={actorId} host={NetworkServer.active} scenario={scenario} actors={_actors.Count}");

            if (scenario.StartsWith("solo_", StringComparison.Ordinal))
            {
                yield return RunSolo(scenario);
            }
            else if (NetworkServer.active)
            {
                yield return RunHost(scenario == "final", players - 1);
            }
            else
            {
                yield return RunCitizen(actorId, scenario == "final");
            }

            Log($"ALL_DONE actor={actorId} scenario={scenario} pass={_passCount} fail={_failCount}");
        }

        private IEnumerator RunHost(bool isFinal, int citizens)
        {
            _round.RequestStart(TowerTrialRound.MafiaPick.Requester);
            yield return WaitFor("round_started_as_mafia",
                () => _round.IsStarted && _round.HasOwnRole && _round.IsOwnMafia && _round.HasVoting, StateTimeoutSeconds);
            yield return MoveTo(new Vector3(2f, 0f, 2f));
            int stoneBefore = Held(Stone);
            _grant.RequestGrantAll();
            yield return WaitFor("granted_once", () => Held(Stone) >= stoneBefore + 10, StateTimeoutSeconds);
            _grant.RequestGrantAll();
            yield return WaitFor("granted_twice", () => Held(Stone) >= stoneBefore + 20, StateTimeoutSeconds);

            // Mafia withdraw: base resources come back and an emptied recipe frees its module record.
            int stone = Held(Stone);
            yield return SendAndExpect("mafia_deposit", _workbench.netId, InteractionAction.Deposit, FrameRecipe, Stone, 1, InteractionResultCode.Success);
            yield return SendAndExpect("mafia_withdraw", _workbench.netId, InteractionAction.Withdraw, FrameRecipe, Stone, 1, InteractionResultCode.Success);
            yield return WaitFor("withdraw_restored", () => Held(Stone) == stone && !_workbench.IsStarted(FrameRecipe), StateTimeoutSeconds);
            yield return SendAndExpect("withdraw_empty", _workbench.netId, InteractionAction.Withdraw, FrameRecipe, Stone, 1, InteractionResultCode.Rejected);

            yield return BuildFloor(FrameRecipe, GearRecipe);

            if (!isFinal)
            {
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(LensRecipe, CoreRecipe);
                Check("tower_complete", _match.Tower.IsComplete, Describe());
            }

            // The check succeeds only once enough citizens point at the mafia; the server copy shows when they have.
            yield return WaitFor("citizens_voted", () => _round.CountCitizenVotesOnMafia() >= citizens, 90f);
            yield return Identity("identity_check", _voting.RequestIdentityCheck, IdentityActionResult.CheckSucceeded);

            if (!isFinal)
            {
                yield return SendAndExpect("start_ritual", _match.netId, InteractionAction.StartRitual, 0, 0, 0, InteractionResultCode.Success);
                yield return WaitFor("ritual_phase", () => _match.Phase == TowerPhase.Ritual, StateTimeoutSeconds);
                yield return WaitFor("ritual_clock_stopped", () => _round.IsClockStopped, StateTimeoutSeconds);
                yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
                Check("ritual_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RitualSucceeded, Describe());
                yield break;
            }

            yield return Identity("summon", _voting.RequestSummon, IdentityActionResult.SummonApproved);
            yield return WaitFor("final_phase", () => _match.Phase == TowerPhase.FinalBattle, StateTimeoutSeconds);
            // The first floor (2 of 8 slots) stays; the health is the fixed maximum, not a share of the repair.
            Check("summon_carryover", _match.Tower.InstalledCount == 2 && Mathf.Approximately(_match.MaxHealth, _match.Settings.MaxHealth)
                && _match.Health <= _match.MaxHealth && _match.Health > _match.MaxHealth * 0.5f, Describe());
            // Nothing may be stored yet, so this refusal alone does not prove the closed hands; the panel test does.
            yield return SendAndExpect("mafia_take_closed", _workbench.netId, InteractionAction.TakeModule, FrameRecipe, 0, 0, InteractionResultCode.Rejected);
            yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
            Check("final_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RepairCompleted
                && _match.Health > 0f, Describe());
        }

        private IEnumerator RunCitizen(uint actorId, bool isFinal)
        {
            yield return WaitFor("round_started_as_citizen", () => _round.IsStarted && _round.HasOwnRole && !_round.IsOwnMafia, StartTimeoutSeconds);
            int rank = CitizenRank(actorId);
            yield return MoveTo(new Vector3(-1f - rank, 0f, 2f));
            yield return SendAndExpect("citizen_withdraw", _workbench.netId, InteractionAction.Withdraw, FrameRecipe, Stone, 1, InteractionResultCode.Rejected);

            uint suspect = _actors[0];
            _hasVoteReply = false;
            _voting.RequestVote(suspect);
            yield return WaitFor("voted", () => _voting.HasOwnVoteState && _voting.OwnVoteState.TargetActorId == suspect, StateTimeoutSeconds);

            if (!isFinal)
            {
                yield return WaitFor("ritual_seen", () => _match.Phase == TowerPhase.Ritual, PhaseTimeoutSeconds);
                yield return WaitFor("votes_locked", () => _voting.OwnVoteState.IsLocked, StateTimeoutSeconds);
                uint other = _actors[_actors.Count - 1] != actorId ? _actors[_actors.Count - 1] : _actors[1];
                _hasVoteReply = false;
                _voting.RequestVote(other);
                yield return WaitFor("vote_change_refused", () => _hasVoteReply && _voteReply == VoteChangeResult.VotingLocked, StateTimeoutSeconds);
                yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
                Check("ritual_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RitualSucceeded
                    && _round.RevealedMafiaId == suspect, Describe());
                yield break;
            }

            yield return WaitFor("final_seen", () => _match.Phase == TowerPhase.FinalBattle, PhaseTimeoutSeconds);
            Check("summon_carryover_synced", _match.Tower.InstalledCount >= 2 && Mathf.Approximately(_match.MaxHealth, _match.Settings.MaxHealth), Describe());
            Check("mafia_revealed", _round.RevealedMafiaId == suspect, Describe());

            // One citizen finishes the repair, so the floors go in order without two builders racing for a slot.
            if (rank == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    int stone = Held(Stone);
                    _grant.RequestGrantAll();
                    yield return WaitFor($"granted{i}", () => Held(Stone) > stone, StateTimeoutSeconds);
                }

                yield return MoveTo(new Vector3(2f, 0f, 1.5f));
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(LensRecipe, CoreRecipe);
            }

            yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
            Check("final_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RepairCompleted
                && _match.Tower.IsComplete, Describe());
        }

        private IEnumerator RunSolo(string scenario)
        {
            bool isMafia = scenario == "solo_mafia";
            _round.RequestStart(isMafia ? TowerTrialRound.MafiaPick.Requester : TowerTrialRound.MafiaPick.Random);
            yield return WaitFor("round_started", () => _round.IsStarted && _round.HasOwnRole && _round.IsOwnMafia == isMafia, StateTimeoutSeconds);
            yield return MoveTo(new Vector3(2f, 0f, 2f));
            yield return GrantFor(scenario == "solo_mafia"
                ? new[] { FrameRecipe, GearRecipe, FrameRecipe }
                : new[] { FrameRecipe, GearRecipe, FrameRecipe, GearRecipe, FrameRecipe, GearRecipe, LensRecipe, CoreRecipe });
            yield return BuildFloor(FrameRecipe, GearRecipe);

            if (scenario == "solo_full")
            {
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(FrameRecipe, GearRecipe);
                yield return BuildFloor(LensRecipe, CoreRecipe);
                Check("tower_complete", _match.Tower.IsComplete && _match.Phase == TowerPhase.Repairing, Describe());
                _round.RequestForceSummon();
                yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, StateTimeoutSeconds);
                Check("full_summon_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RepairCompleted
                    && Mathf.Approximately(_match.MaxHealth, 0f), Describe());
                yield break;
            }

            if (isMafia)
            {
                yield return Craft(FrameRecipe);
                yield return WaitFor("frame_stored", () => _workbench.GetStoredCount(FrameRecipe) > 0, StateTimeoutSeconds);
            }

            _round.RequestForceSummon();
            yield return WaitFor("final_phase", () => _match.Phase == TowerPhase.FinalBattle, StateTimeoutSeconds);
            Check("summon_carryover", _match.Tower.InstalledCount == 2 && Mathf.Approximately(_match.MaxHealth, _match.Settings.MaxHealth)
                && _match.Health > _match.MaxHealth * 0.5f, Describe());

            if (isMafia)
            {
                yield return SendAndExpect("mafia_take_closed", _workbench.netId, InteractionAction.TakeModule, FrameRecipe, 0, 0, InteractionResultCode.Rejected);
                Check("mafia_hands_empty", _workbench.OwnCarriedRecipe == 0 && _workbench.GetStoredCount(FrameRecipe) > 0, Describe());
                yield break;
            }

            yield return BuildFloor(FrameRecipe, GearRecipe);
            yield return BuildFloor(FrameRecipe, GearRecipe);
            Check("health_not_healed", _match.Health <= _match.MaxHealth && Mathf.Approximately(_match.MaxHealth, _match.Settings.MaxHealth), Describe());
            yield return BuildFloor(LensRecipe, CoreRecipe);
            yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, StateTimeoutSeconds);
            Check("final_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.RepairCompleted
                && _match.Health > 0f, Describe());
        }

        // Asks for the dev grant until the inventory holds every ingredient of these recipes together.
        private IEnumerator GrantFor(int[] recipeIds)
        {
            var needs = new Dictionary<int, int>();

            for (int i = 0; i < recipeIds.Length; i++)
            {
                if (!_workbench.Catalog.TryGetById(recipeIds[i], out Lighthouse.Workbench.Rules.ModuleRecipe recipe))
                {
                    continue;
                }

                for (int j = 0; j < recipe.IngredientCount; j++)
                {
                    Lighthouse.Workbench.Rules.RecipeIngredient ingredient = recipe.GetIngredient(j);
                    needs.TryGetValue(ingredient.ResourceId, out int amount);
                    needs[ingredient.ResourceId] = amount + ingredient.Amount;
                }
            }

            for (int i = 0; i < 20 && !HasAll(needs); i++)
            {
                int stone = Held(Stone);
                _grant.RequestGrantAll();
                yield return WaitFor($"granted{i}", () => Held(Stone) > stone, StateTimeoutSeconds);
            }

            Check("resources_enough", HasAll(needs), Describe());
        }

        private bool HasAll(Dictionary<int, int> needs)
        {
            foreach (KeyValuePair<int, int> need in needs)
            {
                if (Held(need.Key) < need.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerator BuildFloor(int firstRecipe, int secondRecipe)
        {
            yield return Craft(firstRecipe);
            yield return Carry(firstRecipe);
            yield return SendAndExpect($"install_{firstRecipe}", _match.netId, InteractionAction.InstallModule, firstRecipe, 0, 0, InteractionResultCode.Success);
            yield return Craft(secondRecipe);
            yield return Carry(secondRecipe);
            yield return SendAndExpect($"install_{secondRecipe}", _match.netId, InteractionAction.InstallModule, secondRecipe, 0, 0, InteractionResultCode.Success);
            yield return WaitFor("hands_empty", () => _workbench.OwnCarriedRecipe == 0, StateTimeoutSeconds);
        }

        // Modules reach the tower by hand (2026-10-20). The first carry also checks the full hand and putting it back.
        private IEnumerator Carry(int recipeId)
        {
            yield return WaitFor($"stored_{recipeId}", () => _workbench.GetStoredCount(recipeId) > 0, StateTimeoutSeconds);
            yield return SendAndExpect($"take_{recipeId}", _workbench.netId, InteractionAction.TakeModule, recipeId, 0, 0, InteractionResultCode.Success);
            yield return WaitFor($"carried_{recipeId}", () => _workbench.OwnCarriedRecipe == recipeId, StateTimeoutSeconds);

            if (_hasCheckedReturn)
            {
                yield break;
            }

            _hasCheckedReturn = true;
            yield return SendAndExpect("take_while_carrying", _workbench.netId, InteractionAction.TakeModule, recipeId, 0, 0, InteractionResultCode.Rejected);
            yield return SendAndExpect("return", _workbench.netId, InteractionAction.ReturnModule, 0, 0, 0, InteractionResultCode.Success);
            yield return WaitFor("returned",
                () => _workbench.OwnCarriedRecipe == 0 && _workbench.GetStoredCount(recipeId) > 0, StateTimeoutSeconds);
            yield return SendAndExpect("return_empty", _workbench.netId, InteractionAction.ReturnModule, 0, 0, 0, InteractionResultCode.Rejected);
            yield return SendAndExpect("take_again", _workbench.netId, InteractionAction.TakeModule, recipeId, 0, 0, InteractionResultCode.Success);
            yield return WaitFor("carried_again", () => _workbench.OwnCarriedRecipe == recipeId, StateTimeoutSeconds);
        }

        private IEnumerator Craft(int recipeId)
        {
            if (!_workbench.Catalog.TryGetById(recipeId, out Lighthouse.Workbench.Rules.ModuleRecipe recipe))
            {
                Check($"recipe_{recipeId}", false, "missing");
                yield break;
            }

            for (int i = 0; i < recipe.IngredientCount; i++)
            {
                Lighthouse.Workbench.Rules.RecipeIngredient ingredient = recipe.GetIngredient(i);
                yield return SendAndExpect($"deposit_{recipeId}_{ingredient.ResourceId}", _workbench.netId, InteractionAction.Deposit,
                    recipeId, ingredient.ResourceId, ingredient.Amount, InteractionResultCode.Success);
            }

            yield return SendAndExpect($"craft_{recipeId}", _workbench.netId, InteractionAction.Craft, recipeId, 0, 0, InteractionResultCode.Success);
        }

        private IEnumerator Identity(string step, Action request, IdentityActionResult expected)
        {
            _hasIdentityDecision = false;
            request();
            yield return WaitFor(step, () => _hasIdentityDecision && _identityDecision == expected, StateTimeoutSeconds);
        }

        private IEnumerator MoveTo(Vector3 position)
        {
            Transform player = NetworkClient.localPlayer.transform;
            CharacterController controller = player.GetComponent<CharacterController>();

            if (controller)
            {
                controller.enabled = false;
            }

            player.position = position;

            if (controller)
            {
                controller.enabled = true;
            }

            yield return new WaitForSeconds(MoveSettleSeconds);
        }

        private IEnumerator SendAndExpect(string step, uint targetNetId, InteractionAction action, int arg0, int arg1, int arg2,
            InteractionResultCode expected)
        {
            _hasReply = false;
            _awaitedSequence = _relay.NextSequenceForTest;

            if (!_relay.TrySendToNetworkTarget(targetNetId, action, arg0, arg1, arg2))
            {
                Check(step, false, "send refused");
                yield break;
            }

            double deadline = Time.realtimeSinceStartupAsDouble + ReplyTimeoutSeconds;

            while (!_hasReply && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Check(step, _hasReply && _reply.Code == expected, _hasReply ? $"code={_reply.Code} expected={expected}" : "no reply");
        }

        private IEnumerator WaitFor(string step, Func<bool> condition, float timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;

            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Check(step, condition(), Describe());
        }

        private int RefreshActors()
        {
            _actors.Clear();

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity && identity.TryGetComponent(out NetworkActor actor) && actor.ActorId != 0)
                {
                    _actors.Add(actor.ActorId);
                }
            }

            _actors.Sort();
            return _actors.Count;
        }

        // Position among the citizens (every actor but the lowest), so each stands in its own spot.
        private int CitizenRank(uint actorId)
        {
            int rank = _actors.IndexOf(actorId) - 1;
            return rank < 0 ? 0 : rank;
        }

        private int Held(int resourceId)
        {
            return _inventory ? _inventory.GetSyncedCount(resourceId) : 0;
        }

        private string Describe()
        {
            return $"phase={_match.Phase} outcome={_match.Outcome} reason={_match.EndReason} hp={_match.Health:0.#}/{_match.MaxHealth:0} " +
                $"installed={_match.Tower.InstalledCount} " +
                $"day={_round.Day} mafia={_round.RevealedMafiaId} vote={(_voting.HasOwnVoteState ? _voting.OwnVoteState.TargetActorId : 0)} " +
                $"locked={(_voting.HasOwnVoteState && _voting.OwnVoteState.IsLocked)}";
        }

        private void HandleResultReceived(InteractionResult result)
        {
            if (result.Sequence == _awaitedSequence)
            {
                _reply = result;
                _hasReply = true;
            }
        }

        private void HandleIdentityResponded(IdentityResponse response)
        {
            if (response.IsNewDecision)
            {
                _identityDecision = response.Result;
                _hasIdentityDecision = true;
                Log($"IDENTITY {response.Result}");
            }
        }

        private void HandleVoteResponded(VoteResponse response)
        {
            if (response.Result != VoteChangeResult.StateRetrieved)
            {
                _voteReply = response.Result;
                _hasVoteReply = true;
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

        private static string ReadArg(string prefix, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return args[i].Substring(prefix.Length);
                }
            }

            return fallback;
        }

        private static void Log(string message)
        {
            Debug.Log($"[{nameof(TowerTrialAutomation)}] {message}");
        }
    }
}
#endif
