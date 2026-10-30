using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.GhostShip
{
    // JH04 trial GUI for the ghost ship hint (W22, 2026-10-30). The real clock waits five minutes for a night and then
    // rolls a chance, so the host can raise or sink the ship here. Every window sees the phase and its distance to the
    // ship, so a tester knows when the inspect key is in range. Inspecting itself stays on the real input.
    [DisallowMultipleComponent]
    public sealed class GhostShipTestPanel : MonoBehaviour
    {
        private const float Width = 260f;

        [SerializeField]
        private GhostShipNet _ship;
        [SerializeField]
        private float _top = 300f;

        private void OnEnable()
        {
            DevPanelSwitch.Register();
        }

        private void OnDisable()
        {
            DevPanelSwitch.Unregister();
        }

        private void OnGUI()
        {
            if (!DevPanelSwitch.IsOn || !_ship || !_ship.isActiveAndEnabled || !NetworkClient.active)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10f, _top, Width, 120f), GUI.skin.box);
            GUILayout.Label($"유령선: {PhaseName(_ship.Phase)}  거리 {Distance()}");

            if (NetworkServer.active)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = _ship.Phase == GhostShipPhase.Hidden;

                if (GUILayout.Button("띄우기"))
                {
                    _ship.Summon();
                }

                GUI.enabled = _ship.Phase == GhostShipPhase.Rising || _ship.Phase == GhostShipPhase.Roaming;

                if (GUILayout.Button("가라앉히기"))
                {
                    _ship.Dismiss();
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("띄우기·가라앉히기는 호스트 창에서");
            }

            GUILayout.EndArea();
        }

        private string Distance()
        {
            NetworkIdentity player = NetworkClient.localPlayer;
            return player ? $"{Vector3.Distance(player.transform.position, _ship.transform.position):0}m" : "-";
        }

        private static string PhaseName(GhostShipPhase phase)
        {
            switch (phase)
            {
                case GhostShipPhase.Rising:
                    return "떠오르는 중";
                case GhostShipPhase.Roaming:
                    return "배회 중";
                case GhostShipPhase.Sinking:
                    return "가라앉는 중";
                default:
                    return "숨음";
            }
        }
    }
}
