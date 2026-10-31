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
        [SerializeField]
        private LayerMask _interactionLayers;

        private InteractionRelay _relay;
        private PlayerController _player;
        private InteractionTarget _selected;

        // Local player only. For UI highlight (최명기); null when nothing is in range.
        public InteractionTarget Selected => _selected;
        public InteractionRelay Relay => _relay;

        public event Action<InteractionTarget> SelectedChanged;

        private void Awake()
        {
            _relay = GetComponent<InteractionRelay>();
            _player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                return;
            }

            RefreshSelection();
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
            if (!isLocalPlayer)
            {
                return false;
            }
            RefreshSelection();

            if (!_selected)
            {
                return false;
            }
            return _relay.TrySendToNetworkTarget(_selected.NetId, _selected.DefaultAction);
        }

        // Stops the running held interaction, e.g. on key release. False when there is none.
        [Client]
        public bool CancelSession()
        {
            return isLocalPlayer && _relay.TryCancelSession();
        }

        private void RefreshSelection()
        {
            SetSelected(FindNearest(transform.position));
        }

        private void SetSelected(InteractionTarget target)
        {
            if (ReferenceEquals(_selected, target))
            {
                return;
            }

            _selected = target;
            SelectedChanged?.Invoke(_selected);
        }

        private InteractionTarget FindNearest(Vector3 origin)
        {
            IReadOnlyList<InteractionTarget> targets = InteractionTarget.Active;
            // The boat being ridden is always nearest, so it would take E from everything at sea such as the ghost
            // ship (2026-10-31). Its repair stays on the inventory screen's button, which sends its own request.
            Transform riddenBoat = _player && _player.CurrentBoat ? _player.CurrentBoat.transform : null;
            InteractionTarget nearest = null;
            float nearestSqr = float.PositiveInfinity;

            for (int i = 0; i < targets.Count; i++)
            {
                InteractionTarget target = targets[i];

                if (!target || target.NetId == 0 || (riddenBoat && target.transform.IsChildOf(riddenBoat)))
                {
                    continue;
                }

                int targetLayerMask = 1 << target.gameObject.layer;
                if ((_interactionLayers.value & targetLayerMask) == 0)
                {
                    continue;
                }

                if (!target.Supports(target.DefaultAction))
                {
                    continue;
                }

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
