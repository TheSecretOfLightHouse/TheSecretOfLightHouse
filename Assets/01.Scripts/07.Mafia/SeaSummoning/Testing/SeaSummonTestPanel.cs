using Lighthouse.Mafia.SeaSummoning.Net;
using Lighthouse.Mafia.SeaSummoning.View;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Testing
{
    public sealed class SeaSummonTestPanel : MonoBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonCameraView _cameraView;
        [SerializeField] private SeaSummonPointView _pointView;
        [SerializeField] private SeaSummonRadialMenu _radialMenu;

        private void OnGUI()
        {
            if (_radialMenu != null && _radialMenu.IsOpen)
            {
                return;
            }
            GUILayout.BeginArea(new Rect(15, 110, 460, 360), GUI.skin.box);
            GUILayout.Label("마피아 바다 몬스터 소환 — 4단계");
            GUILayout.Label("시험 권한: 배에 탑승한 호스트의 Tech 1만 허용");
            PlayerController player = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<PlayerController>() : null;
            GUILayout.Label(player != null && player.IsOnBoat ? "현재 상태: 배 탑승 중" : "현재 상태: 미탑승");
            if (_permission != null && _permission.Settings != null)
            {
                GUILayout.Label($"하루 {_permission.Settings.DailyLimit}회 / 탑뷰 높이 {_permission.Settings.TopViewHeight}");
            }
            GUI.enabled = NetworkClient.ready;
            if (player != null && player.OwnedBoat != null && GUILayout.Button("승선 / 하선 시험"))
            {
                if (player.IsOnBoat)
                {
                    player.OwnedBoat.RequestDisembark(player);
                }
                else
                {
                    player.TryBoard(player.OwnedBoat);
                }
            }
            if (GUILayout.Button("서버에 내 소환 권한 확인"))
            {
                _permission.RequestOwnPermission();
            }
            GUI.enabled = true;
            GUILayout.Label(_permission != null && _permission.HasPermissionResponse
                ? (_permission.HasOwnPermission ? "현재 사용 가능: 서버 허용·탑승 확인" : "현재 사용 불가: 미탑승 또는 서버 거절") : "서버 응답 대기");
            GUILayout.Label("하선은 출항 보호 종료 후 육지 인접 상태에서 가능");
            GUILayout.Label(_cameraView != null ? _cameraView.Status : "카메라 미연결");
            GUILayout.Label(_pointView != null ? _pointView.Status : "지점 선택 미연결");
            GUILayout.Label("초록: 선택 가능 / 빨강: 불가 / 청록: 서버 승인");
            GUILayout.Label("노란 원: 안전지대 / 부두: 육지");
            GUILayout.Label(_radialMenu != null ? _radialMenu.Status : "종류 선택 메뉴 미연결");
            GUILayout.Label("하루 횟수 제한·날짜 전환 제거는 5단계");
            GUILayout.EndArea();
        }
    }
}
