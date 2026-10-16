using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.WorldState.Sandbox
{
    public class NetMessageCapture : MonoBehaviour
    {
        private readonly Dictionary<string, Entry> _sent = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Entry> _received = new Dictionary<string, Entry>();
        private bool _isCapturing;

        private void OnDisable()
        {
            Unsubscribe();
        }

        [ContextMenu("NetPacket/Start Capture")]
        private void StartCapture()
        {
            Unsubscribe();
            _sent.Clear();
            _received.Clear();
            NetworkDiagnostics.OutMessageEvent += HandleSent;
            NetworkDiagnostics.InMessageEvent += HandleReceived;
            _isCapturing = true;
            Debug.Log($"[{nameof(NetMessageCapture)}] capture started");
        }

        [ContextMenu("NetPacket/Stop And Log")]
        private void StopAndLog()
        {
            Unsubscribe();
            Log("sent", _sent);
            Log("received", _received);
        }

        private void Unsubscribe()
        {
            if (!_isCapturing)
            {
                return;
            }

            NetworkDiagnostics.OutMessageEvent -= HandleSent;
            NetworkDiagnostics.InMessageEvent -= HandleReceived;
            _isCapturing = false;
        }

        private void HandleSent(NetworkDiagnostics.MessageInfo info)
        {
            Add(_sent, info);
        }

        private void HandleReceived(NetworkDiagnostics.MessageInfo info)
        {
            Add(_received, info);
        }

        private static void Add(Dictionary<string, Entry> table, NetworkDiagnostics.MessageInfo info)
        {
            string key = info.message.GetType().Name;
            if (!table.TryGetValue(key, out Entry entry))
            {
                entry = new Entry();
                table.Add(key, entry);
            }

            entry.Messages++;
            entry.Targets += info.count;
            entry.Bytes += (long)info.bytes * info.count;
        }

        private static void Log(string label, Dictionary<string, Entry> table)
        {
            foreach (KeyValuePair<string, Entry> pair in table)
            {
                Debug.Log($"[{nameof(NetMessageCapture)}] {label} {pair.Key}: messages={pair.Value.Messages} targets={pair.Value.Targets} bytes={pair.Value.Bytes}");
            }
        }

        private sealed class Entry
        {
            public int Messages;
            public int Targets;
            public long Bytes;
        }
    }
}
