using System.Text;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public class IslandNetProbe : MonoBehaviour
    {
        private void OnEnable()
        {
            IslandStateNet.ClientStarted += HandleClientStarted;
        }

        private void OnDisable()
        {
            IslandStateNet.ClientStarted -= HandleClientStarted;

            for (int i = 0; i < IslandStateNet.Active.Count; i++)
            {
                IslandStateNet.Active[i].NodeChanged -= HandleNodeChanged;
            }
        }

        [ContextMenu("IslandNet/Log Nodes")]
        private void LogAll()
        {
            for (int i = 0; i < IslandStateNet.Active.Count; i++)
            {
                LogIsland("state", IslandStateNet.Active[i]);
            }
        }

        private void HandleClientStarted(IslandStateNet island)
        {
            island.NodeChanged += HandleNodeChanged;
            LogIsland("started", island);
        }

        private void HandleNodeChanged(IslandStateNet island, int index, NodeState node)
        {
            Debug.Log($"[{nameof(IslandNetProbe)}] {Role()} island={island.IslandIndex} node={index} res={node.ResourceId} remaining={node.Remaining}");
        }

        private static void LogIsland(string tag, IslandStateNet island)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < island.Nodes.Count; i++)
            {
                NodeState node = island.Nodes[i];
                builder.Append(i).Append(':').Append(node.ResourceId).Append('/').Append(node.Remaining).Append(' ');
            }

            Debug.Log($"[{nameof(IslandNetProbe)}] {Role()} {tag} island={island.IslandIndex} count={island.Nodes.Count} nodes=[{builder}]");
        }

        private static string Role()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return "Host";
            }

            if (NetworkServer.active)
            {
                return "Server";
            }

            return NetworkClient.active ? "Client" : "Offline";
        }
    }
}
