using System;
using System.Collections.Generic;
using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Targets.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Relay.View
{
    // Picks the nearest interaction target around the local player and sends its default action on request.
    // Input sources stay outside (2026-10-14): the real player adapter and test adapters both call TryInteract.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionRelay))]
    public sealed class InteractionSelector : NetworkBehaviour
    {
        private InteractionRelay _relay;
        private InteractionTarget _selected;

        // Local player only. For UI highlight (최명기); null when nothing is in range.
        public InteractionTarget Selected => _selected;
        public InteractionRelay Relay => _relay;

        public event Action<InteractionTarget> SelectedChanged;

        private void Awake()
        {
            _relay = GetComponent<InteractionRelay>();
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                return;
            }

            InteractionTarget nearest = FindNearest(transform.position);

            if (nearest != _selected)
            {
                _selected = nearest;
                SelectedChanged?.Invoke(_selected);
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (_selected)
            {
                _selected = null;
                SelectedChanged?.Invoke(null);
            }
        }

        // Returns false when nothing is selected or the previous request is still waiting for its reply.
        [Client]
        public bool TryInteract()
        {
            if (!isLocalPlayer || !_selected)
            {
                return false;
            }

            return _relay.TrySendToNetworkTarget(_selected.NetId, _selected.DefaultAction);
        }

        private static InteractionTarget FindNearest(Vector3 origin)
        {
            IReadOnlyList<InteractionTarget> targets = InteractionTarget.Active;
            InteractionTarget nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                InteractionTarget target = targets[i];
                float rangeSqr = target.Range * target.Range;
                float distanceSqr = (target.Position - origin).sqrMagnitude;

                if (distanceSqr <= rangeSqr && distanceSqr < nearestSqr)
                {
                    nearest = target;
                    nearestSqr = distanceSqr;
                }
            }

            return nearest;
        }
    }
}
