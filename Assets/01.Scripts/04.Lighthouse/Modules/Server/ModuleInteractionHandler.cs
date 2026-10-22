using System;
using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Modules.Rules;
using Lighthouse.Tower.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Modules.Server
{
    // Server entry for the module actions. The router takes one handler per action, so this one handler
    // branches on the target's component (workbench, lighthouse and boat now; drifting modules with JH05).
    // It also owns this match's module ledger, since the existence cap spans every place a module can be.
    //   Deposit:       Arg0 = recipe id, Arg1 = resource id, Arg2 = requested amount (capped to what the recipe still needs).
    //   Craft:         Arg0 = recipe id; finishes that recipe once every ingredient is in.
    //   TakeModule:    Arg0 = recipe id; one stored module of it goes into the sender's hands. One per person (2026-10-20).
    //   ReturnModule:  puts the carried module back into the bench it came from.
    //   InstallModule: Arg0 = recipe id of the carried module. On the lighthouse the lowest open slot takes it;
    //                  on the sender's own boat the boat installs its kind.
    //   Withdraw:      Arg0 = recipe id, Arg1 = resource id, Arg2 = amount. Mafia only, base resources only.
    // Each recipe keeps its own progress (2026-10-18), so there is no SelectRecipe step on the workbench.
    // The lighthouse takes modules only (2026-10-17), so Deposit never reaches it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class ModuleInteractionHandler : NetworkBehaviour, IInteractionHandler
    {
        private static readonly InteractionAction[] HandledActions =
        {
            InteractionAction.Deposit,
            InteractionAction.Craft,
            InteractionAction.InstallModule,
            InteractionAction.Withdraw,
            InteractionAction.TakeModule,
            InteractionAction.ReturnModule
        };

        private readonly bool[] _isRegistered = new bool[HandledActions.Length];
        // Server-only. Which bench each carried module came from, so a boat install (whose target is the boat)
        // finds it, and so one-per-person holds across benches.
        private readonly Dictionary<uint, WorkbenchNet> _carrySources = new Dictionary<uint, WorkbenchNet>();
        // Server-only. Boats with modules on them, watched for wrecks.
        private readonly Dictionary<BoatDurability, BoatModules> _boats = new Dictionary<BoatDurability, BoatModules>();
        private ModuleLedger _ledger;

        // Server-only. Valid between OnStartServer and OnStopServer.
        public ModuleLedger Ledger => _ledger;
        // Server-only. Answers whether an actor is the mafia; roles belong to the match, not here. Unset rejects every withdraw.
        public Func<uint, bool> MafiaCheck { get; set; }
        // Server-only. Answers whether an actor may still take a module into their hands; the match closes the revealed
        // mafia's inventory (2026-10-20). Unset lets everyone carry.
        public Func<uint, bool> CarryCheck { get; set; }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _ledger = new ModuleLedger();

            for (int i = 0; i < HandledActions.Length; i++)
            {
                _isRegistered[i] = InteractionRouter.RegisterHandler(HandledActions[i], this);
            }
        }

        public override void OnStopServer()
        {
            for (int i = 0; i < HandledActions.Length; i++)
            {
                if (_isRegistered[i])
                {
                    InteractionRouter.UnregisterHandler(HandledActions[i], this);
                    _isRegistered[i] = false;
                }
            }

            foreach (BoatModules boat in _boats.Values)
            {
                boat.Unbind();
            }

            _boats.Clear();
            _carrySources.Clear();
            MafiaCheck = null;
            CarryCheck = null;
            _ledger = null;
            base.OnStopServer();
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!isServer || _ledger == null || context.Sender == null || !context.Sender.identity)
            {
                return InteractionResultCode.Rejected;
            }

            PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

            if (!player || player.IsDowned)
            {
                return InteractionResultCode.Rejected;
            }

            NetworkIdentity target = context.Target.Identity;

            if (!target)
            {
                return InteractionResultCode.InvalidTarget;
            }

            // A boat install happens aboard (boat module decision); the boat itself checks owner and boarding.
            if (context.Action == InteractionAction.InstallModule && target.TryGetComponent(out BoatModuleEquipment equipment))
            {
                return HandleBoatInstall(context, player, equipment);
            }

            // Same body check as picking up: a player aboard a boat cannot work the bench or the tower.
            if (player.IsOnBoat)
            {
                return InteractionResultCode.Rejected;
            }

            if (context.Action == InteractionAction.InstallModule)
            {
                return target.TryGetComponent(out TowerNet tower) ? HandleTowerInstall(context, tower) : InteractionResultCode.InvalidTarget;
            }

            if (!target.TryGetComponent(out WorkbenchNet workbench))
            {
                return InteractionResultCode.InvalidTarget;
            }

            switch (context.Action)
            {
                case InteractionAction.Deposit:
                    return HandleDeposit(context, workbench);
                case InteractionAction.Craft:
                    return workbench.TryCraft(context.ActorId, context.Arg0, _ledger) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
                case InteractionAction.Withdraw:
                    return HandleWithdraw(context, workbench);
                case InteractionAction.TakeModule:
                    return HandleTake(context, workbench);
                case InteractionAction.ReturnModule:
                    return HandleReturn(context, workbench);
                default:
                    return InteractionResultCode.Rejected;
            }
        }

        // Summoning shuts the mafia's inventory, and the module in their hands goes with it (2026-10-20).
        // The record goes too, so the recipe can be made again.
        [Server]
        public bool DiscardCarried(uint actorId)
        {
            if (_ledger == null || !_carrySources.TryGetValue(actorId, out WorkbenchNet workbench))
            {
                return false;
            }

            _carrySources.Remove(actorId);

            if (!workbench || !workbench.TryReleaseCarried(actorId, out _, out uint moduleId))
            {
                return false;
            }

            _ledger.Remove(moduleId);
            return true;
        }

        private InteractionResultCode HandleDeposit(in InteractionContext context, WorkbenchNet workbench)
        {
            // Only the sender's own inventory; a client never names whose resources to spend.
            PlayerInventory inventory = context.Sender.identity.GetComponent<PlayerInventory>();

            if (!inventory)
            {
                return InteractionResultCode.Rejected;
            }

            int moved = workbench.Deposit(context.ActorId, inventory, context.Arg0, context.Arg1, context.Arg2, _ledger);
            return moved > 0 ? InteractionResultCode.Success : InteractionResultCode.Rejected;
        }

        private InteractionResultCode HandleWithdraw(in InteractionContext context, WorkbenchNet workbench)
        {
            // The role is checked here on the server; hiding the button on other clients is not the check.
            if (MafiaCheck == null || !MafiaCheck(context.ActorId))
            {
                return InteractionResultCode.Rejected;
            }

            PlayerInventory inventory = context.Sender.identity.GetComponent<PlayerInventory>();
            int moved = workbench.Withdraw(inventory, context.Arg0, context.Arg1, context.Arg2, _ledger);
            return moved > 0 ? InteractionResultCode.Success : InteractionResultCode.Rejected;
        }

        // Citizens and the mafia alike may take one out; the mafia-only rule is for base resources (2026-10-20).
        private InteractionResultCode HandleTake(in InteractionContext context, WorkbenchNet workbench)
        {
            if ((CarryCheck != null && !CarryCheck(context.ActorId)) || _carrySources.ContainsKey(context.ActorId)
                || !workbench.TryTakeToCarry(context.ActorId, context.Arg0, _ledger))
            {
                return InteractionResultCode.Rejected;
            }

            _carrySources.Add(context.ActorId, workbench);
            return InteractionResultCode.Success;
        }

        // Back to the bench it came from only; the stock is counted per bench.
        private InteractionResultCode HandleReturn(in InteractionContext context, WorkbenchNet workbench)
        {
            if (!_carrySources.TryGetValue(context.ActorId, out WorkbenchNet source) || source != workbench
                || !workbench.TryReturnCarried(context.ActorId, _ledger))
            {
                return InteractionResultCode.Rejected;
            }

            _carrySources.Remove(context.ActorId);
            return InteractionResultCode.Success;
        }

        private InteractionResultCode HandleTowerInstall(in InteractionContext context, TowerNet tower)
        {
            if (!tower.TryInstall(context.ActorId, context.Arg0, _ledger))
            {
                return InteractionResultCode.Rejected;
            }

            _carrySources.Remove(context.ActorId);
            return InteractionResultCode.Success;
        }

        // Everything that can fail is checked before the hands are emptied, so a refused install keeps the module.
        private InteractionResultCode HandleBoatInstall(in InteractionContext context, PlayerController player, BoatModuleEquipment equipment)
        {
            if (!_carrySources.TryGetValue(context.ActorId, out WorkbenchNet workbench) || !workbench
                || !workbench.TryGetCarried(context.ActorId, out int recipeId, out _) || recipeId != context.Arg0
                || !workbench.Catalog.TryGetById(recipeId, out ModuleRecipe recipe) || !recipe.IsBoatModule)
            {
                return InteractionResultCode.Rejected;
            }

            // The boat checks owner, boarding, wreck, support for the kind and a free slot of that kind.
            if (recipe.BoatModuleType == BoatModuleType.None || !equipment.TryInstall(player, recipe.BoatModuleType))
            {
                return InteractionResultCode.Rejected;
            }

            workbench.TryReleaseCarried(context.ActorId, out _, out uint moduleId);
            _carrySources.Remove(context.ActorId);
            _ledger.TrySetState(moduleId, ModuleState.OnBoat);
            WatchBoat(equipment).ModuleIds.Add(moduleId);
            return InteractionResultCode.Success;
        }

        private BoatModules WatchBoat(BoatModuleEquipment equipment)
        {
            BoatDurability durability = equipment.GetComponent<BoatDurability>();

            if (!_boats.TryGetValue(durability, out BoatModules boat))
            {
                boat = new BoatModules(durability, _ledger);
                _boats.Add(durability, boat);
            }

            return boat;
        }

        // A wreck clears every module on the boat (boat module decision, confirmed 2026-10-21), so their records go too
        // and the recipes can be made again. The boat clears its own installed list on the same event.
        private sealed class BoatModules
        {
            public readonly List<uint> ModuleIds = new List<uint>();

            private readonly BoatDurability _durability;
            private readonly ModuleLedger _ledger;

            public BoatModules(BoatDurability durability, ModuleLedger ledger)
            {
                _durability = durability;
                _ledger = ledger;
                _durability.Wrecked += HandleWrecked;
            }

            public void Unbind()
            {
                if (_durability)
                {
                    _durability.Wrecked -= HandleWrecked;
                }
            }

            private void HandleWrecked()
            {
                for (int i = 0; i < ModuleIds.Count; i++)
                {
                    _ledger.Remove(ModuleIds[i]);
                }

                ModuleIds.Clear();
            }
        }
    }
}
