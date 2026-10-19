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
    //   final:  host installs one floor (25%), checks and summons -> health 475; citizens collect crystals and
    //           relight the lantern room four times -> citizens win.
    // Scenario: "-jh04Scenario=final" (default ritual). Players expected: "-jh04Players=3".
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

            if (NetworkServer.active)
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
            // One floor of eight slots is 25%: 300 + 700 x 0.25.
            Check("summon_health", Mathf.Approximately(_match.Health, 475f) && Mathf.Approximately(_match.MaxHealth, 1000f), Describe());
            yield return SendAndExpect("install_closed", _match.netId, InteractionAction.InstallModule, FrameRecipe, 0, 0, InteractionResultCode.Rejected);
            AbyssCrystalSpot spot = SortedSpots()[5];
            yield return MoveTo(spot.transform.position + Vector3.right);
            yield return SendAndExpect("mafia_collect", spot.netId, InteractionAction.CollectAbyssCrystal, 0, 0, 0, InteractionResultCode.Rejected);
            yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
            Check("final_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.LanternRelit
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
            Check("summon_health_synced", Mathf.Approximately(_match.Health, 475f) || _match.Health < 475f, Describe());
            Check("mafia_revealed", _round.RevealedMafiaId == suspect, Describe());
            _grant.RequestGrantAll();
            yield return new WaitForSeconds(1f);
            AbyssCrystalSpot[] spots = SortedSpots();

            // Each citizen takes two spots of its own, then relights twice.
            for (int i = 0; i < 2; i++)
            {
                AbyssCrystalSpot spot = spots[rank * 2 + i];
                yield return WaitFor($"crystal{i}_available", () => spot.IsAvailable, StateTimeoutSeconds);
                yield return MoveTo(spot.transform.position + Vector3.right);
                int carried = _match.OwnCrystalCount;
                yield return SendAndExpect($"collect{i}", spot.netId, InteractionAction.CollectAbyssCrystal, 0, 0, 0, InteractionResultCode.Success);
                yield return WaitFor($"carried{i}", () => _match.OwnCrystalCount == carried + 1, StateTimeoutSeconds);
            }

            yield return MoveTo(new Vector3(2f + rank, 0f, 1.5f));

            for (int i = 0; i < 2; i++)
            {
                if (_match.Phase != TowerPhase.FinalBattle)
                {
                    break;
                }

                yield return SendAndExpect($"ignite{i}", _match.netId, InteractionAction.IgniteLantern, 0, 0, 0, InteractionResultCode.Success);
            }

            yield return WaitFor("ended", () => _match.Phase == TowerPhase.Ended, PhaseTimeoutSeconds);
            Check("final_citizens_win", _match.Outcome == TowerOutcome.CitizensWin && _match.EndReason == TowerEndReason.LanternRelit, Describe());
        }

        private IEnumerator BuildFloor(int firstRecipe, int secondRecipe)
        {
            yield return Craft(firstRecipe);
            yield return SendAndExpect($"install_{firstRecipe}", _match.netId, InteractionAction.InstallModule, firstRecipe, 0, 0, InteractionResultCode.Success);
            yield return Craft(secondRecipe);
            yield return SendAndExpect($"install_{secondRecipe}", _match.netId, InteractionAction.InstallModule, secondRecipe, 0, 0, InteractionResultCode.Success);
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

        // Position among the citizens (every actor but the lowest), so each takes different crystal spots.
        private int CitizenRank(uint actorId)
        {
            int rank = _actors.IndexOf(actorId) - 1;
            return rank < 0 ? 0 : rank;
        }

        private AbyssCrystalSpot[] SortedSpots()
        {
            AbyssCrystalSpot[] spots = FindObjectsByType<AbyssCrystalSpot>(FindObjectsSortMode.None);
            Array.Sort(spots, (a, b) => string.CompareOrdinal(a.name, b.name));
            return spots;
        }

        private int Held(int resourceId)
        {
            return _inventory ? _inventory.GetSyncedCount(resourceId) : 0;
        }

        private string Describe()
        {
            return $"phase={_match.Phase} outcome={_match.Outcome} reason={_match.EndReason} hp={_match.Health:0.#}/{_match.MaxHealth:0} " +
                $"ignitions={_match.Ignitions} carried={_match.OwnCrystalCount} installed={_match.Tower.InstalledCount} " +
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
