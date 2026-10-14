using System.Collections.Generic;
using Lighthouse.Interaction.Requests.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Interaction.Targets.Net
{
    // Marks a network object as something players can interact with. It registers nothing: the object is
    // identified by its netId, which is unique within a match (2026-10-14 decision).
    // Clients use it to pick a target; the server uses the same data to check the requested action and range.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class InteractionTarget : MonoBehaviour
    {
        private static readonly List<InteractionTarget> ActiveTargets = new List<InteractionTarget>();

        [SerializeField]
        private InteractionAction _defaultAction = InteractionAction.None;
        [SerializeField]
        private InteractionAction[] _supportedActions = new InteractionAction[0];
        [Tooltip("Distance from the player within which clients may select this target.")]
        [SerializeField, Min(0f)]
        private float _range = 3f;
        [Tooltip("Extra distance the server allows, because it sees the player's reported position late by the interpolation delay. Test value.")]
        [SerializeField, Min(0f)]
        private float _serverRangeSlack = 1.5f;

        private NetworkIdentity _identity;

        // Every enabled target on this machine. Read-only scan for client selection; small enough to iterate.
        public static IReadOnlyList<InteractionTarget> Active => ActiveTargets;

        public uint NetId => _identity.netId;
        public InteractionAction DefaultAction => _defaultAction;
        public float Range => _range;
        public float ServerRange => _range + _serverRangeSlack;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _identity = GetComponent<NetworkIdentity>();
        }

        private void OnEnable()
        {
            ActiveTargets.Add(this);
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        public bool Supports(InteractionAction action)
        {
            if (action == InteractionAction.None)
            {
                return false;
            }

            for (int i = 0; i < _supportedActions.Length; i++)
            {
                if (_supportedActions[i] == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
