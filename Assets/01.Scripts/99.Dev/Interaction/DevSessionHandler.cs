#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Sessions.Rules;
using Lighthouse.Interaction.Sessions.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Interaction
{
    // JH02 A-2 test session handler standing in for harvest/rescue: registered for one existing action in the Jihun test
    // scene only. Arg0 is the duration in milliseconds (test-only meaning). Targets labelled "Vanish..." are despawned
    // shortly after a session starts, to check the target-lost path.
    [DisallowMultipleComponent]
    public sealed class DevSessionHandler : NetworkBehaviour, IInteractionSessionHandler
    {
        private const string VanishPrefix = "Vanish";
        private const int MinDurationMs = 100;
        private const int MaxDurationMs = 20000;

        [SerializeField]
        private InteractionAction _action = InteractionAction.Harvest;
        [SerializeField, Min(0.1f)]
        private float _defaultDurationSeconds = 1.5f;
        [SerializeField, Min(0f)]
        private float _vanishDelaySeconds = 0.5f;

        private bool _isRegistered;

        public bool IsExclusive => true;

        public override void OnStartServer()
        {
            _isRegistered = InteractionRouter.RegisterSessionHandler(_action, this);
            InteractionSessions.SessionEnded += HandleSessionEnded;
        }

        public override void OnStopServer()
        {
            InteractionSessions.SessionEnded -= HandleSessionEnded;

            if (_isRegistered)
            {
                InteractionRouter.UnregisterSessionHandler(_action, this);
                _isRegistered = false;
            }
        }

        public InteractionResultCode Begin(in InteractionContext context, out float durationSeconds)
        {
            durationSeconds = 0f;

            if (!TryGetCounter(context, out DevInteractionCounter counter))
            {
                return InteractionResultCode.Rejected;
            }

            if (context.Arg0 != 0 && (context.Arg0 < MinDurationMs || context.Arg0 > MaxDurationMs))
            {
                return InteractionResultCode.Rejected;
            }

            durationSeconds = context.Arg0 != 0 ? context.Arg0 / 1000f : _defaultDurationSeconds;
            counter.SetOccupied(true);

            if (counter.Label.StartsWith(VanishPrefix))
            {
                StartCoroutine(DespawnLater(counter.gameObject));
            }

            Debug.Log($"[{nameof(DevSessionHandler)}] Begin actor={context.ActorId} target={counter.Label} duration={durationSeconds}");
            return InteractionResultCode.Success;
        }

        public bool CanContinue(in InteractionSession session)
        {
            return true;
        }

        public void Complete(in InteractionSession session)
        {
            if (TryGetCounter(session.Context, out DevInteractionCounter counter))
            {
                counter.SetOccupied(false);
                int count = counter.Increment();
                Debug.Log($"[{nameof(DevSessionHandler)}] Complete actor={session.ActorId} session={session.SessionId} target={counter.Label} count={count}");
            }
        }

        public void Cancel(in InteractionSession session, InteractionSessionEndReason reason)
        {
            if (TryGetCounter(session.Context, out DevInteractionCounter counter))
            {
                counter.SetOccupied(false);
            }
        }

        private static bool TryGetCounter(in InteractionContext context, out DevInteractionCounter counter)
        {
            counter = null;
            return context.Target.Identity && context.Target.Identity.TryGetComponent(out counter);
        }

        private IEnumerator DespawnLater(GameObject target)
        {
            yield return new WaitForSeconds(_vanishDelaySeconds);

            if (target)
            {
                NetworkServer.Destroy(target);
            }
        }

        private void HandleSessionEnded(InteractionSession session, InteractionSessionEndReason reason)
        {
            Debug.Log($"[{nameof(DevSessionHandler)}] Ended actor={session.ActorId} session={session.SessionId} reason={reason}");
        }
    }
}
#endif
