#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Interaction
{
    // JH02 test handler standing in for a real feature: registered for one existing action in the Jihun test scene only.
    // The shared InteractionAction enum gets no test-only value.
    [DisallowMultipleComponent]
    public sealed class DevInteractionHandler : NetworkBehaviour, IInteractionHandler
    {
        [SerializeField]
        private InteractionAction _action = InteractionAction.PickUp;

        private bool _isRegistered;

        public override void OnStartServer()
        {
            _isRegistered = InteractionRouter.RegisterHandler(_action, this);
        }

        public override void OnStopServer()
        {
            if (_isRegistered)
            {
                InteractionRouter.UnregisterHandler(_action, this);
                _isRegistered = false;
            }
        }

        public InteractionResultCode Handle(in InteractionContext context)
        {
            if (!context.Target.Identity || !context.Target.Identity.TryGetComponent(out DevInteractionCounter counter))
            {
                return InteractionResultCode.Rejected;
            }

            int count = counter.Increment();
            Debug.Log($"[{nameof(DevInteractionHandler)}] Applied actor={context.ActorId} target={counter.Label} count={count}");
            return InteractionResultCode.Success;
        }
    }
}
#endif
