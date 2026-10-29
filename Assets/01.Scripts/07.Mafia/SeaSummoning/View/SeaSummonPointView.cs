using Lighthouse.Mafia.SeaSummoning.Net;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [DefaultExecutionOrder(350)]
    public sealed class SeaSummonPointView : MonoBehaviour
    {
        [SerializeField] private SeaSummonCameraView _cameraView;
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private SeaSummonPointController _points;
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private Rect _blockedUiRect = new Rect(15, 110, 460, 360);
        private bool _wasSelecting;
        private bool _hasHover;
        private bool _isHoverValid;
        private Vector3 _hoverPoint;
        private string _hoverReason;
        public bool IsSelectionBlocked { get; set; }
        public string Status => _hasHover && !_isHoverValid ? _hoverReason : _points != null && _points.HasOwnSelection
            ? _points.OwnResult : (_hasHover ? _hoverReason : "탑뷰에서 바다를 클릭해 주세요");

        private void LateUpdate()
        {
            bool selecting = _cameraView != null && _cameraView.IsTopViewActive && !_cameraView.IsTransitioning;
            if (!selecting)
            {
                ClearSelection();
                return;
            }
            _wasSelecting = true;
            if (_viewCamera == null)
            {
                _viewCamera = Camera.main;
            }
            _hasHover = false;
            if (IsSelectionBlocked)
            {
                return;
            }
            if (_points == null || _permission == null || Mouse.current == null || _viewCamera == null
                || _points.Validator == null || !WorldServices.TryGet<IWorldQuery>(out var world)
                || NetworkClient.localPlayer == null)
            {
                return;
            }
            Vector2 screen = Mouse.current.position.ReadValue();
            if (!_viewCamera.pixelRect.Contains(screen)
                || _blockedUiRect.Contains(new Vector2(screen.x, Screen.height - screen.y))
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }
            Ray ray = _viewCamera.ScreenPointToRay(screen);
            float height = world.SampleWaterHeight(ray.origin);
            Vector3 candidate = default;
            // 파도 수면 높이를 몇 차례 다시 조회해 표시 지점을 수면에 맞춘다.
            for (int i = 0; i < 4; i++)
            {
                Plane surface = new Plane(Vector3.up, new Vector3(0f, height, 0f));
                if (!surface.Raycast(ray, out float distance))
                {
                    return;
                }
                candidate = ray.GetPoint(distance);
                height = world.SampleWaterHeight(candidate);
            }
            PlayerController player = NetworkClient.localPlayer.GetComponent<PlayerController>();
            if (player.CurrentBoat == null)
            {
                return;
            }
            _hasHover = true;
            _isHoverValid = _points.Validator.TryResolve(candidate, player.CurrentBoat.transform.position,
                _permission.OwnTopViewYaw, _viewCamera.aspect, _permission.Settings, out _hoverPoint, out _hoverReason);
            if (Mouse.current.leftButton.wasPressedThisFrame && _isHoverValid)
            {
                _points.RequestPoint(_hoverPoint, _viewCamera.aspect);
            }
        }

        private void OnDisable()
        {
            ClearSelection();
        }

        private void OnGUI()
        {
            if (!_wasSelecting || _viewCamera == null || IsSelectionBlocked)
            {
                return;
            }
            if (_hasHover)
            {
                DrawMarker(_hoverPoint, _isHoverValid ? Color.green : Color.red, 12f);
            }
            if (_points != null && _points.HasOwnSelection)
            {
                DrawMarker(_points.OwnSelection, Color.cyan, 20f);
            }
        }

        private void ClearSelection()
        {
            if (_wasSelecting && _points != null && NetworkClient.active)
            {
                _points.CancelSelection();
            }
            _wasSelecting = false;
            _hasHover = false;
        }

        private void DrawMarker(Vector3 point, Color color, float radius)
        {
            Vector3 screen = _viewCamera.WorldToScreenPoint(point);
            if (screen.z <= 0f)
            {
                return;
            }
            float x = screen.x;
            float y = Screen.height - screen.y;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - radius, y - radius, radius * 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - radius, y + radius, radius * 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - radius, y - radius, 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + radius, y - radius, 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
