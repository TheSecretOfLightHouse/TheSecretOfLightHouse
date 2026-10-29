using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Network.Session.View;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // JH07 test aid. Put it on the network manager object so it survives scene changes.
    // It steps aside while the real session screens (SessionScreen) are up.
    [DisallowMultipleComponent]
    public sealed class SessionTestPanel : MonoBehaviour
    {
        [SerializeField]
        private LighthouseNetworkManager _manager;
        [SerializeField]
        private Rect _area = new Rect(10f, 10f, 320f, 260f);

        private void Awake()
        {
            if (!_manager)
            {
                _manager = GetComponent<LighthouseNetworkManager>();
            }
        }

        private void OnGUI()
        {
            if (!_manager || SessionScreen.IsShown)
            {
                return;
            }

            GUILayout.BeginArea(_area, GUI.skin.box);

            if (!NetworkClient.active && !NetworkServer.active)
            {
                DrawOffline();
            }
            else
            {
                DrawSession();
            }

            GUILayout.EndArea();
        }

        private void DrawOffline()
        {
            GUILayout.Label("오프라인");
            _manager.networkAddress = GUILayout.TextField(_manager.networkAddress);

            if (GUILayout.Button("호스트 열기"))
            {
                _manager.StartHost();
            }

            if (GUILayout.Button("참가"))
            {
                _manager.StartClient();
            }

            if (_manager.HasSessionEnd)
            {
                SessionEndedMessage end = _manager.LastSessionEnd;
                GUILayout.Label("마지막 종료: " + DescribeEnd(end));
            }
        }

        private void DrawSession()
        {
            SessionStateMessage state = _manager.ClientState;

            switch (state.Phase)
            {
                case MatchPhase.Lobby:
                    DrawLobby();
                    break;
                case MatchPhase.Loading:
                    GUILayout.Label($"게임 씬 로딩 중 {state.LoadedCount}/{state.PlayerCount} (판 #{state.MatchId})");
                    break;
                case MatchPhase.InProgress:
                    GUILayout.Label($"판 진행 중 #{state.MatchId} ({state.PlayerCount}명)");
                    break;
                case MatchPhase.Ended:
                    GUILayout.Label("판 종료 중...");
                    break;
            }

            if (GUILayout.Button(NetworkServer.active ? "나가기 (호스트 종료)" : "나가기"))
            {
                _manager.LeaveSession();
            }
        }

        private void DrawLobby()
        {
            IReadOnlyList<LobbyPlayer> players = LobbyPlayer.Active;
            int readyCount = 0;
            LobbyPlayer localPlayer = null;

            GUILayout.Label($"로비 (시작 인원 {_manager.StartPlayerCount})");

            for (int i = 0; i < players.Count; i++)
            {
                LobbyPlayer player = players[i];

                if (player.IsReady)
                {
                    readyCount++;
                }

                if (player.isLocalPlayer)
                {
                    localPlayer = player;
                }

                GUILayout.Label($"Actor {player.ActorId}: {(player.IsReady ? "준비" : "대기")}{(player.isLocalPlayer ? " (나)" : string.Empty)}");
            }

            GUILayout.Label($"준비 {readyCount}/{players.Count}");

            if (localPlayer && GUILayout.Button(localPlayer.IsReady ? "준비 취소" : "준비"))
            {
                localPlayer.RequestSetReady(!localPlayer.IsReady);
            }
        }

        private static string DescribeEnd(SessionEndedMessage end)
        {
            switch (end.Reason)
            {
                case SessionEndReason.ParticipantLeft:
                    return $"Actor {end.ActorId}님이 나갔습니다. 판이 종료되었습니다";
                case SessionEndReason.ParticipantDisconnected:
                    return $"Actor {end.ActorId}님의 연결이 끊겼습니다. 판이 종료되었습니다";
                case SessionEndReason.LoadTimeout:
                    return "로딩 시간 초과로 판이 종료되었습니다";
                case SessionEndReason.JoinRejected:
                    return "진행 중인 판에는 참가할 수 없습니다";
                case SessionEndReason.ConnectionLost:
                    return "연결이 끊겨 판이 종료되었습니다";
                case SessionEndReason.LobbyConnectionLost:
                    return "연결이 끊겨 로비에서 나왔습니다";
                case SessionEndReason.SelfLeft:
                    return "직접 나갔습니다";
                default:
                    return end.Reason.ToString();
            }
        }
    }
}
