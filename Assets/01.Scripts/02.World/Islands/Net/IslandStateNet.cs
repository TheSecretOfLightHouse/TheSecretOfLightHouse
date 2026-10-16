using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    [DisallowMultipleComponent]
    public class IslandStateNet : NetworkBehaviour
    {
        private static readonly List<IslandStateNet> ActiveIslands = new List<IslandStateNet>();

        [SyncVar] private short _islandIndex;

        private readonly SyncList<NodeState> _nodes = new SyncList<NodeState>();
        private readonly List<NodeState> _buffer = new List<NodeState>();
        private IHarvestChangeSource _source;

        public static IReadOnlyList<IslandStateNet> Active => ActiveIslands;

        public static event Action<IslandStateNet> ClientStarted;

        public event Action<IslandStateNet, int, NodeState> NodeChanged;

        public short IslandIndex => _islandIndex;

        public IReadOnlyList<NodeState> Nodes => _nodes;

        private void Awake()
        {
            _nodes.Callback += HandleNodesChanged;
        }

        private void OnEnable()
        {
            ActiveIslands.Add(this);
        }

        private void OnDisable()
        {
            ActiveIslands.Remove(this);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public override void OnStartClient()
        {
            ClientStarted?.Invoke(this);
        }

        public override void OnStopServer()
        {
            Unbind();
        }

        [Server]
        public bool ServerInitialize(short islandIndex)
        {
            if (!WorldServices.TryGet(out IHarvestChangeSource source) || !source.TryCopyNodes(islandIndex, _buffer))
            {
                return false;
            }

            Unbind();
            _islandIndex = islandIndex;
            _nodes.Clear();
            for (int i = 0; i < _buffer.Count; i++)
            {
                _nodes.Add(_buffer[i]);
            }

            _source = source;
            _source.RemainingChanged += HandleRemainingChanged;
            return true;
        }

        private void Unbind()
        {
            if (_source == null)
            {
                return;
            }

            _source.RemainingChanged -= HandleRemainingChanged;
            _source = null;
        }

        private void HandleRemainingChanged(short islandIndex, int localIndex, int remaining)
        {
            if (islandIndex != _islandIndex || localIndex < 0 || localIndex >= _nodes.Count)
            {
                return;
            }

            if (remaining > ushort.MaxValue)
            {
                Debug.LogWarning($"[{nameof(IslandStateNet)}] island={islandIndex} node={localIndex} remaining={remaining} exceeds {ushort.MaxValue}. Clamped.");
            }

            _nodes[localIndex] = new NodeState(_nodes[localIndex].ResourceId, remaining);
        }

        private void HandleNodesChanged(SyncList<NodeState>.Operation operation, int index, NodeState oldItem, NodeState newItem)
        {
            if (operation == SyncList<NodeState>.Operation.OP_SET)
            {
                NodeChanged?.Invoke(this, index, newItem);
            }
        }
    }
}
