using System;
using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.World.Islands.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.EditorTools
{
    // Server-side connection doubles exercise the real spawn/ownership path.
    // They do not validate a remote client's transport or rendering.
    public static class MainIslandSpawnChecks
    {
        private sealed class VerificationConnection : NetworkConnectionToClient
        {
            public VerificationConnection(int id) : base(id)
            {
            }

            protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable)
            {
            }
        }

        public static void AddThreeLobbyPlayers()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            for (int i = 1; i <= 3; i++)
            {
                var connection = new VerificationConnection(1000 + i);
                connection.isAuthenticated = true;
                NetworkServer.connections.Add(connection.connectionId, connection);
                manager.OnServerAddPlayer(connection);
            }

            foreach (LobbyPlayer player in UnityEngine.Object.FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
            {
                typeof(LobbyPlayer).GetField("_isReady", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(player, true);
            }
            typeof(LighthouseNetworkManager).GetMethod("HandleReadyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
        }

        public static void CompleteThreeGamePlayers()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            Lighthouse.World.MapGeneration.Net.MapStateNet map = UnityEngine.Object.FindFirstObjectByType<Lighthouse.World.MapGeneration.Net.MapStateNet>();
            System.Reflection.MethodInfo method = typeof(Lighthouse.World.MapGeneration.Net.MapStateNet).GetMethod("HandleWorldReady",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 1; i <= 3; i++)
            {
                NetworkConnectionToClient connection = NetworkServer.connections[1000 + i];
                manager.OnServerAddPlayer(connection);
                method.Invoke(map, new object[] { connection, new Lighthouse.World.MapGeneration.Net.WorldReadyMessage
                {
                    LayoutHash = map.Layout.Hash,
                    ContentHash = map.ContentHash
                } });
            }
        }

        public static string AddThreeServerPlayers()
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            MainIslandPlayerSpawns source = UnityEngine.Object.FindFirstObjectByType<MainIslandPlayerSpawns>();
            if (!Application.isPlaying || !NetworkServer.active || !manager || !source || !source.IsReady)
            {
                throw new InvalidOperationException("Start a direct-mode MainGame host and wait for the main island.");
            }

            for (int i = 1; i <= 3; i++)
            {
                int id = 1000 + i;
                if (NetworkServer.connections.ContainsKey(id))
                {
                    throw new InvalidOperationException("Verification connection already exists.");
                }

                var connection = new VerificationConnection(id);
                connection.isAuthenticated = true;
                NetworkServer.connections.Add(id, connection);
                manager.OnServerAddPlayer(connection);
            }

            PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            var jobs = new HashSet<string>();
            var playerPositions = new HashSet<Vector2>();
            var boatPositions = new HashSet<Vector2>();
            foreach (PlayerController player in players)
            {
                BoatController boat = player.OwnedBoat;
                if (!boat || boat.Owner != player || boat.connectionToClient != player.connectionToClient
                    || !player.BoatPrefab || !boat.name.StartsWith(player.BoatPrefab.name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Boat prefab or ownership mismatch.");
                }

                jobs.Add(player.name.Split('[')[0].Trim());
                playerPositions.Add(new Vector2(Mathf.Round(player.transform.position.x), Mathf.Round(player.transform.position.z)));
                boatPositions.Add(new Vector2(Mathf.Round(boat.transform.position.x), Mathf.Round(boat.transform.position.z)));
            }

            if (players.Length != 4 || jobs.Count != 4 || playerPositions.Count != 4 || boatPositions.Count != 4)
            {
                throw new InvalidOperationException("Expected four distinct jobs, player spots and boat spots.");
            }

            bool fifthAccepted = source.TryReserve(9999, out _, out _);
            if (fifthAccepted)
            {
                throw new InvalidOperationException("A fifth actor reserved an occupied spot.");
            }

            return "PASS: 4 players, 4 jobs, 4 distinct player spots, 4 distinct boat spots, prefab/owner/connection match, fifth reservation rejected.";
        }
    }
}
