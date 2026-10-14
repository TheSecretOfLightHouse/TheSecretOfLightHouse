using System;
using System.IO;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.WorldState.Sandbox
{
    public class MppmClientDriver : MonoBehaviour
    {
        private const string VirtualProjectsMarker = "/Library/VP/";
        private const string CommandFolder = "Temp/MppmCommands";
        private const string JoinCommand = "join";
        private const string LeaveCommand = "leave";

        [SerializeField, Min(0.1f)] private float _pollInterval = 0.5f;

        private string _commandPath;
        private string _playerId;
        private float _nextPollTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<MppmClientDriver>() != null)
            {
                return;
            }

            new GameObject(nameof(MppmClientDriver)).AddComponent<MppmClientDriver>();
        }

        private void Awake()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            int markerIndex = dataPath.IndexOf(VirtualProjectsMarker, StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                enabled = false;
                return;
            }

            int idStart = markerIndex + VirtualProjectsMarker.Length;
            int idEnd = dataPath.IndexOf('/', idStart);
            _playerId = idEnd > idStart ? dataPath.Substring(idStart, idEnd - idStart) : dataPath.Substring(idStart);

            string folder = Path.Combine(dataPath.Substring(0, markerIndex), CommandFolder);
            Directory.CreateDirectory(folder);
            _commandPath = Path.Combine(folder, _playerId + ".txt");

            Debug.Log($"[{nameof(MppmClientDriver)}] ready id={_playerId} path={_commandPath}");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollTime)
            {
                return;
            }

            _nextPollTime = Time.unscaledTime + _pollInterval;

            if (!File.Exists(_commandPath))
            {
                return;
            }

            string command = File.ReadAllText(_commandPath).Trim().ToLowerInvariant();
            File.Delete(_commandPath);
            Execute(command);
        }

        private void Execute(string command)
        {
            NetworkManager manager = NetworkManager.singleton;

            if (manager == null)
            {
                Debug.LogError($"[{nameof(MppmClientDriver)}] NetworkManager is missing.");
                return;
            }

            if (command == JoinCommand && !NetworkClient.active)
            {
                manager.StartClient();
            }
            else if (command == LeaveCommand && NetworkClient.active)
            {
                manager.StopClient();
            }

            Debug.Log($"[{nameof(MppmClientDriver)}] {_playerId} command={command} clientActive={NetworkClient.active}");
        }
    }
}
