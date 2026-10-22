using Lighthouse.Mafia.SeaSummoning.Net;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Mafia.SeaSummoning.View
{
    [DefaultExecutionOrder(300)]
    public sealed class SeaSummonCameraView : MonoBehaviour
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField, Min(0.1f)] private float _permissionTimeout = 5f;
        private InputAction _toggleAction;
        private PlayerController _player;
        private PlayerInput _playerInput;
        private BoatController _boat;
        private Transform _camera;
        private Transform _savedParent;
        private Vector3 _savedLocalPosition;
        private Quaternion _savedLocalRotation;
        private Vector3 _savedWorldPosition;
        private Quaternion _savedWorldRotation;
        private Vector3 _transitionPosition;
        private Quaternion _transitionRotation;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;
        private bool _wasInputActive;
        private bool _hasCameraControl;
        private bool _isReturning;
        private bool _isAwaitingPermission;
        private float _requestedAt;
        private float _transitionStartedAt;
        private float _topYaw;
        private float _savedFieldOfView;
        private bool _savedOrthographic;

        public bool IsTopViewActive => _hasCameraControl && !_isReturning;
        public bool IsTransitioning => _hasCameraControl && _permission != null && _permission.Settings != null
            && Time.unscaledTime - _transitionStartedAt < _permission.Settings.CameraTransitionSeconds;
        public string Status { get; private set; } = "Q: 탑뷰 진입 / 복귀";

        private void Awake()
        {
            _toggleAction = new InputAction("SeaSummonTopView", InputActionType.Button, "<Keyboard>/q");
            _toggleAction.performed += HandleToggle;
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
            if (_permission != null)
            {
                _permission.PermissionResponded += HandlePermissionResponded;
            }
        }

        private void LateUpdate()
        {
            if (_isAwaitingPermission && Time.unscaledTime - _requestedAt >= _permissionTimeout)
            {
                _isAwaitingPermission = false;
                Status = "서버 응답 시간 초과 — Q로 재시도";
            }
            if (!_hasCameraControl)
            {
                return;
            }
            if (!CanKeepView())
            {
                RestoreView();
                Status = "탑승 상태 변경으로 탑뷰 종료";
                return;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - _transitionStartedAt) / _permission.Settings.CameraTransitionSeconds);
            float blend = Mathf.SmoothStep(0f, 1f, t);
            Vector3 targetPosition = _isReturning
                ? _savedParent.TransformPoint(_savedLocalPosition)
                : _boat.transform.position + Vector3.up * _permission.Settings.TopViewHeight;
            Quaternion targetRotation = _isReturning
                ? _savedParent.rotation * _savedLocalRotation : Quaternion.Euler(90f, _topYaw, 0f);
            _camera.SetPositionAndRotation(Vector3.Lerp(_transitionPosition, targetPosition, blend),
                Quaternion.Slerp(_transitionRotation, targetRotation, blend));
            if (_isReturning && t >= 1f)
            {
                RestoreView();
                Status = "Q: 탑뷰 진입 / 복귀";
            }
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
            if (_permission != null)
            {
                _permission.PermissionResponded -= HandlePermissionResponded;
            }
            _isAwaitingPermission = false;
            RestoreView();
        }

        private void OnDestroy()
        {
            _toggleAction.Dispose();
        }

        public void RequestToggle()
        {
            if (_hasCameraControl)
            {
                if (!_isReturning)
                {
                    _isReturning = true;
                    StartTransition();
                    Status = "플레이어 시점으로 복귀 중";
                }
                return;
            }
            if (_isAwaitingPermission || _permission == null || !NetworkClient.ready)
            {
                return;
            }
            _isAwaitingPermission = true;
            _requestedAt = Time.unscaledTime;
            Status = "서버에 탑뷰 진입 권한 확인 중";
            _permission.RequestOwnPermission();
        }

        private void HandleToggle(InputAction.CallbackContext context)
        {
            RequestToggle();
        }

        private void HandlePermissionResponded(bool allowed)
        {
            if (!_isAwaitingPermission)
            {
                return;
            }
            _isAwaitingPermission = false;
            if (!allowed || !_permission.HasOwnPermission || NetworkClient.localPlayer == null)
            {
                Status = "진입 거절 — 소환 권한과 배 탑승 상태 확인";
                return;
            }
            _player = NetworkClient.localPlayer.GetComponent<PlayerController>();
            _playerInput = _player.GetComponent<PlayerInput>();
            Camera mainCamera = Camera.main;
            if (_playerInput == null || !_playerInput.inputIsActive || mainCamera == null
                || mainCamera.transform.parent == null)
            {
                Status = "진입 불가 — 다른 화면 또는 카메라 사용 중";
                return;
            }

            _boat = _player.CurrentBoat;
            _camera = mainCamera.transform;
            _savedParent = _camera.parent;
            _savedLocalPosition = _camera.localPosition;
            _savedLocalRotation = _camera.localRotation;
            _savedWorldPosition = _camera.position;
            _savedWorldRotation = _camera.rotation;
            _savedFieldOfView = mainCamera.fieldOfView;
            _savedOrthographic = mainCamera.orthographic;
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = _permission.Settings.TopViewFieldOfView;
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _wasInputActive = _playerInput.inputIsActive;
            // 현재 액션 맵을 중지하면 이동 취소 입력도 전달되어 직전 조종 값이 남지 않는다.
            _playerInput.DeactivateInput();
            _boat.Stop();
            _camera.SetParent(null, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _topYaw = _permission.OwnTopViewYaw;
            _hasCameraControl = true;
            _isReturning = false;
            StartTransition();
            Status = "탑뷰 — 배 조종 중지 / Q로 복귀";
        }

        private bool CanKeepView()
        {
            return NetworkClient.ready && NetworkClient.localPlayer != null && _player != null
                && NetworkClient.localPlayer.gameObject == _player.gameObject && _permission != null
                && _permission.Settings != null && _permission.HasOwnPermission && _boat != null && _player.CurrentBoat == _boat
                && _camera != null && _savedParent != null;
        }

        private void StartTransition()
        {
            _transitionPosition = _camera.position;
            _transitionRotation = _camera.rotation;
            _transitionStartedAt = Time.unscaledTime;
        }

        private void RestoreView()
        {
            if (!_hasCameraControl)
            {
                return;
            }
            _hasCameraControl = false;
            _isReturning = false;
            bool isSameLocalPlayer = NetworkClient.localPlayer != null && _player != null
                && NetworkClient.localPlayer.gameObject == _player.gameObject;
            if (_camera != null)
            {
                Camera cameraComponent = _camera.GetComponent<Camera>();
                cameraComponent.fieldOfView = _savedFieldOfView;
                cameraComponent.orthographic = _savedOrthographic;
                // 접속 종료 때 파괴될 플레이어에게 카메라를 다시 붙이지 않는다.
                _camera.SetParent(isSameLocalPlayer ? _savedParent : null, false);
                if (isSameLocalPlayer && _savedParent != null)
                {
                    _camera.localPosition = _savedLocalPosition;
                    _camera.localRotation = _savedLocalRotation;
                }
                else
                {
                    _camera.SetPositionAndRotation(_savedWorldPosition, _savedWorldRotation);
                }
            }
            if (_playerInput != null && _wasInputActive && _playerInput.isActiveAndEnabled
                && isSameLocalPlayer)
            {
                _playerInput.ActivateInput();
            }
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            _player = null;
            _playerInput = null;
            _boat = null;
            _camera = null;
            _savedParent = null;
        }
    }
}
