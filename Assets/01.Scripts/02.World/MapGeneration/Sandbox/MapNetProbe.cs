using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public class MapNetProbe : MonoBehaviour
    {
        [SerializeField] private MapStateNet _state;
        [SerializeField] private MapGenerationConfig _config;

        private bool _wasLoaded;
        private int _lastReadyCount = -1;

        private void Update()
        {
            if (_state == null)
            {
                return;
            }

            bool isLoaded = _state.Layout != null;

            if (isLoaded == _wasLoaded && _state.ReadyCount == _lastReadyCount)
            {
                return;
            }

            _wasLoaded = isLoaded;
            _lastReadyCount = _state.ReadyCount;
            LogState();
        }

        [ContextMenu("MapNet/Log State")]
        private void LogState()
        {
            if (_state == null)
            {
                return;
            }

            MapSeedInfo info = _state.Info;
            MapLayout layout = _state.Layout;
            string local = layout != null ? $"0x{layout.Hash:X8}" : "none";
            int islands = layout != null ? layout.Islands.Count : 0;
            int reefs = layout != null ? layout.Reefs.Count : 0;

            Debug.Log($"[{nameof(MapNetProbe)}] {Role()} set={info.IsSet} seed={info.Seed} sent=0x{info.LayoutHash:X8} local={local} content=0x{_state.ContentHash:X8}/0x{info.ContentHash:X8} islands={islands} reefs={reefs} ready={_state.ReadyCount}");
        }

        [ContextMenu("MapNet/Run Checksum Checks")]
        private void RunChecks()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapNetProbe)}] MapGenerationConfig is not assigned.");
                return;
            }

            Debug.Log($"[{nameof(MapNetProbe)}] {(MapChecksumChecks.RunAll(_config) ? "ALL PASS" : "FAILED")}");
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
