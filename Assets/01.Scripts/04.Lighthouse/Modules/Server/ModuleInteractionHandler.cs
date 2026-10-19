using System;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Tower.Net;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Modules.Server
{
    // Server entry for the module actions. The router takes one handler per action, so this one handler
    // branches on the target's component (workbench and lighthouse now; drifting modules with JH05).
    // It also owns this match's module ledger, since the existence cap spans every place a module can be.
    //   Deposit:       Arg0 = recipe id, Arg1 = resource id, Arg2 = requested amount (capped to what the recipe still needs).
    //   Craft:         Arg0 = recipe id; finishes that recipe once every ingredient is in.
    //   InstallModule: Arg0 = recipe id; the lighthouse picks the lowest open slot for it.
    //   Withdraw:      Arg0 = recipe id, Arg1 = resource id, Arg2 = amount. Mafia only, base resources only.
    // Each recipe keeps its own progress (2026-10-18), so there is no SelectRecipe step on the workbench.
    // The lighthouse takes modules only (2026-10-17), so Deposit never reaches it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class ModuleInteractionHandler : NetworkBehaviour, IInteractionHandler
    {
        private ModuleLedger _ledger;
        private bool _isDepositRegistered;
        private bool _isCraftRegistered;
        private bool _isInstallRegistered;
        private bool _isWithdrawRegistered;

        // Server-only. Valid between OnStartServer and OnStopServer.
        public ModuleLedger Ledger => _ledger;
        // Server-only. Answers whether an actor is the mafia; roles belong to the match, not here. Unset rejects every withdraw.
        public Func<uint, bool> MafiaCheck { get; set; }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _ledger = new ModuleLedger();
            _isDepositRegistered = InteractionRouter.RegisterHandler(InteractionAction.Deposit, this);
            _isCraftRegistered = InteractionRouter.RegisterHandler(InteractionAction.Craft, this);
            _isInstallRegistered = InteractionRouter.RegisterHandler(InteractionAction.InstallModule, this);
            _isWithdrawRegistered = InteractionRouter.RegisterHandler(InteractionAction.Withdraw, this);
        }

        public override void OnStopServer()
        {
            if (_isDepositRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.Deposit, this);
                _isDepositRegistered = false;
            }

            if (_isCraftRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.Craft, this);
                _isCraftRegistered = false;
            }

            if (_isInstallRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.InstallModule, this);
                _isInstallRegistered = false;
            }

            if (_isWithdrawRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.Withdraw, this);
                _isWithdrawRegistered = false;
            }

            MafiaCheck = null;
            _ledger = null;
            base.OnStopServer();
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!isServer || _ledger == null || context.Sender == null || !context.Sender.identity)
            {
                return InteractionResultCode.Rejected;
            }

            // Same body checks as picking up: a downed player or one aboard a boat cannot work the bench or the tower.
            PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

            if (!player || player.IsDowned || player.IsOnBoat)
            {
                return InteractionResultCode.Rejected;
            }

            NetworkIdentity target = context.Target.Identity;

            if (!target)
            {
                return InteractionResultCode.InvalidTarget;
            }

            switch (context.Action)
            {
                case InteractionAction.Deposit:
                    return target.TryGetComponent(out WorkbenchNet depositBench)
                        ? HandleDeposit(context, depositBench)
                        : InteractionResultCode.InvalidTarget;
                case InteractionAction.Craft:
                    if (!target.TryGetComponent(out WorkbenchNet craftBench))
                    {
                        return InteractionResultCode.InvalidTarget;
                    }

                    return craftBench.TryCraft(context.ActorId, context.Arg0, _ledger) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
                case InteractionAction.InstallModule:
                    if (!target.TryGetComponent(out TowerNet tower))
                    {
                        return InteractionResultCode.InvalidTarget;
                    }

                    return tower.TryInstall(context.ActorId, context.Arg0, _ledger) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
                case InteractionAction.Withdraw:
                    if (!target.TryGetComponent(out WorkbenchNet withdrawBench))
                    {
                        return InteractionResultCode.InvalidTarget;
                    }

                    return HandleWithdraw(context, withdrawBench);
                default:
                    return InteractionResultCode.Rejected;
            }
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
    }
}
