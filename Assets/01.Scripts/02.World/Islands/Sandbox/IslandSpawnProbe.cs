using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public class IslandSpawnProbe : MonoBehaviour
    {
        [SerializeField] private IslandWorldHost _host;
        [SerializeField] private IslandInstanceSet _instances;

        private readonly List<HarvestSpot> _spots = new List<HarvestSpot>();
        private int _loggedVersion;

        private void Awake()
        {
            if (_host == null || _instances == null)
            {
                Debug.LogError($"[{nameof(IslandSpawnProbe)}] IslandWorldHost and IslandInstanceSet are required.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_host.BuildVersion == _loggedVersion)
            {
                return;
            }

            _loggedVersion = _host.BuildVersion;
            LogFingerprint();
        }

        [ContextMenu("Islands/Log Fingerprint")]
        private void LogFingerprint()
        {
            StableHash hash = new StableHash();
            int spotTotal = 0;
            hash.Add(_instances.Count);
            for (int i = 0; i < _instances.Count; i++)
            {
                short index = _instances.GetIndex(i);
                Transform island = _instances.GetTransform(i);
                hash.Add(index);
                AddVector(hash, island.position);
                hash.Add(island.eulerAngles.y);
                AddText(hash, island.name);
                spotTotal += AddSpots(hash, index);
            }

            Debug.Log($"[{nameof(IslandSpawnProbe)}] server={NetworkServer.active} client={NetworkClient.active} islands={_instances.Count} spots={spotTotal} version={_host.BuildVersion} fingerprint=0x{hash.Value:X8}");
        }

        private int AddSpots(StableHash hash, short islandIndex)
        {
            if (!_instances.TryGetSpots(islandIndex, _spots))
            {
                return 0;
            }

            for (int i = 0; i < _spots.Count; i++)
            {
                hash.Add(_spots[i].SpotId);
                hash.Add(_spots[i].ResourceId);
                AddVector(hash, _spots[i].Position);
            }

            return _spots.Count;
        }

        private static void AddVector(StableHash hash, Vector3 value)
        {
            hash.Add(value.x);
            hash.Add(value.y);
            hash.Add(value.z);
        }

        private static void AddText(StableHash hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash.Add(value[i]);
            }
        }
    }
}
