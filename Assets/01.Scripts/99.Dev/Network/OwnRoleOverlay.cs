#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // Test aid for the mafia assignment (2026-10-22): with "-roleOverlay" each window shows the role it received,
    // so a multi-window test can see that exactly one player is the mafia. Creates itself; no scene setup.
    public sealed class OwnRoleOverlay : MonoBehaviour
    {
        private const string OverlayFlag = "-roleOverlay";

        private LighthouseNetworkManager _manager;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), OverlayFlag) < 0)
            {
                return;
            }

            var overlay = new GameObject(nameof(OwnRoleOverlay));
            DontDestroyOnLoad(overlay);
            overlay.AddComponent<OwnRoleOverlay>();
        }

        private void Update()
        {
            // The manager is replaced when the offline scene loads, so follow whichever one is current.
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager == _manager)
            {
                return;
            }

            if (_manager)
            {
                _manager.OwnRoleReceived -= HandleOwnRoleReceived;
            }

            _manager = manager;

            if (_manager)
            {
                _manager.OwnRoleReceived += HandleOwnRoleReceived;
            }
        }

        private void OnDestroy()
        {
            if (_manager)
            {
                _manager.OwnRoleReceived -= HandleOwnRoleReceived;
            }
        }

        private void OnGUI()
        {
            if (!_manager || _manager.OwnRole == MatchRole.None)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            string role = _manager.OwnRole == MatchRole.Mafia ? "마피아" : "시민";
            GUI.Box(new Rect(Screen.width * 0.5f - 200f, 10f, 400f, 56f), $"Actor {GetLocalActorId()} · 내 역할: {role}", _style);
        }

        private void HandleOwnRoleReceived(MatchRole role)
        {
            Debug.Log($"[{nameof(OwnRoleOverlay)}] Actor {GetLocalActorId()} received role {role}.");
        }

        private static uint GetLocalActorId()
        {
            return NetworkClient.localPlayer && NetworkClient.localPlayer.TryGetComponent(out NetworkActor actor) ? actor.ActorId : 0;
        }
    }
}
#endif
