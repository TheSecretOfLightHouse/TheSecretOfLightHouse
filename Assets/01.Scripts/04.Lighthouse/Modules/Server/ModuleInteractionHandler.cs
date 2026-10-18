using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Workbench.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Modules.Server
{
    // Server entry for the module actions. The router takes one handler per action, so this one handler
    // branches on the target's component (workbench now; lighthouse and drifting modules with JH04·JH05).
    // It also owns this match's module ledger, since the existence cap spans every place a module can be.
    //   SelectRecipe: Arg0 = recipe id.
    //   Deposit:      Arg0 = resource id, Arg1 = requested amount (capped to what the recipe still needs).
    //   Craft:        no arguments; finishes the current recipe once every ingredient is in.
    // The lighthouse takes modules only (2026-10-17), so Deposit never reaches it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class ModuleInteractionHandler : NetworkBehaviour, IInteractionHandler
    {
        private ModuleLedger _ledger;
        private bool _isSelectRegistered;
        private bool _isDepositRegistered;
        private bool _isCraftRegistered;

        // Server-only. Valid between OnStartServer and OnStopServer.
        public ModuleLedger Ledger => _ledger;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _ledger = new ModuleLedger();
            _isSelectRegistered = InteractionRouter.RegisterHandler(InteractionAction.SelectRecipe, this);
            _isDepositRegistered = InteractionRouter.RegisterHandler(InteractionAction.Deposit, this);
            _isCraftRegistered = InteractionRouter.RegisterHandler(InteractionAction.Craft, this);
        }

        public override void OnStopServer()
        {
            if (_isSelectRegistered)
            {
                InteractionRouter.UnregisterHandler(InteractionAction.SelectRecipe, this);
                _isSelectRegistered = false;
            }

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

            _ledger = null;
            base.OnStopServer();
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!isServer || _ledger == null || context.Sender == null || !context.Sender.identity)
            {
                return InteractionResultCode.Rejected;
            }

            // Same body checks as picking up: a downed player or one aboard a boat cannot work the bench.
            PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

            if (!player || player.IsDowned || player.IsOnBoat)
            {
                return InteractionResultCode.Rejected;
            }

            if (!context.Target.Identity || !context.Target.Identity.TryGetComponent(out WorkbenchNet workbench))
            {
                return InteractionResultCode.InvalidTarget;
            }

            switch (context.Action)
            {
                case InteractionAction.SelectRecipe:
                    return workbench.TrySelectRecipe(context.Arg0, _ledger) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
                case InteractionAction.Deposit:
                    return HandleDeposit(context, workbench);
                case InteractionAction.Craft:
                    return workbench.TryCraft(context.ActorId, _ledger) ? InteractionResultCode.Success : InteractionResultCode.Rejected;
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

            int moved = workbench.Deposit(context.ActorId, inventory, context.Arg0, context.Arg1, _ledger);
            return moved > 0 ? InteractionResultCode.Success : InteractionResultCode.Rejected;
        }
    }
}
