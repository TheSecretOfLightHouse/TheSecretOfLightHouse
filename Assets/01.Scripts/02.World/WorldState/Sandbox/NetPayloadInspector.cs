using System;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.WorldState.Sandbox
{
    public class NetPayloadInspector : MonoBehaviour
    {
        private const int MaxHexBytes = 64;

        [ContextMenu("NetPacket/Dump Initial State")]
        private void DumpInitialState()
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning($"[{nameof(NetPayloadInspector)}] Start the server first.");
                return;
            }

            int inspected = 0;
            int failures = 0;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                NetworkBehaviour[] behaviours = identity.NetworkBehaviours;
                for (int i = 0; i < behaviours.Length; i++)
                {
                    inspected++;
                    if (!Inspect(identity, behaviours[i]))
                    {
                        failures++;
                    }
                }
            }

            Debug.Log($"[{nameof(NetPayloadInspector)}] {(failures == 0 ? "ALL EXACT" : "FAILED")} (components={inspected}, failures={failures})");
        }

        private static bool Inspect(NetworkIdentity identity, NetworkBehaviour behaviour)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                behaviour.OnSerialize(writer, true);
                ArraySegment<byte> bytes = writer.ToArraySegment();
                NetworkReader reader = new NetworkReader(bytes);
                bool isDecoded = NetPayloadDecoder.TryDecode(behaviour, reader, out string text);
                int remaining = isDecoded ? reader.Remaining : 0;
                int hexLength = Mathf.Min(bytes.Count, MaxHexBytes);
                string hex = hexLength > 0 ? BitConverter.ToString(bytes.Array, bytes.Offset, hexLength) : "-";
                string verdict = !isDecoded ? "undecoded" : remaining == 0 ? "EXACT" : $"FAIL extra={remaining}";

                Debug.Log($"[{nameof(NetPayloadInspector)}] {identity.name}/{behaviour.GetType().Name} bytes={bytes.Count} {verdict} {text} hex={hex}");
                return remaining == 0;
            }
        }
    }
}
