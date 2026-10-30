using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Lighthouse.Tower.View
{
    // Camera move for the lighthouse screen (2026-10-30): from the player's view the main camera swings out to a wide
    // shot of the whole lighthouse, seen from the player's side, and the screen opens once it arrives; closing swings
    // back. Cinemachine only drives the camera during the move: the brain stays disabled otherwise, so PlayerCamera
    // keeps owning it. Runs after PlayerCamera so the player camera's pose this frame is what the move starts from and
    // returns to.
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class TowerFocusCamera : MonoBehaviour
    {
        [Tooltip("Stands in for the player's view; follows the main camera's pose each frame.")]
        [SerializeField]
        private CinemachineCamera _playerView;
        [SerializeField]
        private CinemachineCamera _towerView;

        [Header("Shot")]
        [Tooltip("Lighthouse height above the tower's origin; the shot fits this whole height.")]
        [SerializeField, Min(0.1f)]
        private float _lighthouseHeight = 9f;
        [Tooltip("Where the shot aims, as a fraction of the height.")]
        [SerializeField, Range(0f, 1f)]
        private float _aimHeight = 0.5f;
        [Tooltip("Room around the lighthouse; 1 fits it edge to edge.")]
        [SerializeField, Min(1f)]
        private float _framePadding = 1.35f;
        [Tooltip("Degrees the shot looks down at the aim point.")]
        [SerializeField, Range(-30f, 60f)]
        private float _elevation = 8f;
        [Tooltip("Where across the screen the lighthouse stands, 0 left to 1 right. Left of centre leaves the right side for the screen.")]
        [SerializeField, Range(0.1f, 0.9f)]
        private float _screenX = 0.3f;

        [Header("Blend")]
        [SerializeField, Min(0f)]
        private float _focusSeconds = 1.1f;
        [SerializeField, Min(0f)]
        private float _returnSeconds = 0.8f;

        private CinemachineBrain _brain;
        private Transform _output;
        private Action _arrived;
        private Action _returned;
        private bool _isActive;
        private bool _isFocused;

        // Away from the player's view: moving to the lighthouse, holding on it or on the way back.
        public bool IsActive => _isActive;

        private void Awake()
        {
            _playerView.enabled = false;
            _towerView.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_isActive)
            {
                return;
            }

            if (!_brain || !_output)
            {
                Finish();
                return;
            }

            _playerView.transform.SetPositionAndRotation(_output.position, _output.rotation);
            _brain.ManualUpdate();

            if (_brain.IsBlending)
            {
                return;
            }

            if (_isFocused)
            {
                if (_arrived != null && ReferenceEquals(_brain.ActiveVirtualCamera, _towerView))
                {
                    Action arrived = _arrived;
                    _arrived = null;
                    arrived();
                }
            }
            else if (ReferenceEquals(_brain.ActiveVirtualCamera, _playerView))
            {
                Finish();
            }
        }

        private void OnDisable()
        {
            Finish();
        }

        // Moves to the lighthouse standing at tower; arrived runs once the shot settles. Called again while returning,
        // the move turns around from where the camera is and the pending return is dropped.
        public bool Focus(Transform tower, Action arrived)
        {
            Camera camera = Camera.main;

            if (!isActiveAndEnabled || !tower || !camera)
            {
                return false;
            }

            if (!_isActive)
            {
                _brain = camera.GetComponent<CinemachineBrain>();

                if (!_brain)
                {
                    _brain = camera.gameObject.AddComponent<CinemachineBrain>();
                }

                _output = camera.transform;
                _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                _brain.IgnoreTimeScale = true;
                _playerView.Lens = LensSettings.FromCamera(camera);
                _towerView.Lens = _playerView.Lens;
                _playerView.transform.SetPositionAndRotation(_output.position, _output.rotation);
                _playerView.enabled = true;
                _brain.enabled = true;
                // Nothing was live, so this cuts to the player's view: the move below starts from exactly there.
                _brain.ManualUpdate();
                _isActive = true;
            }

            PlaceTowerView(tower, _output.position, camera.fieldOfView, camera.aspect);
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _focusSeconds);
            _towerView.enabled = true;
            _towerView.Prioritize();
            _isFocused = true;
            _arrived = arrived;
            _returned = null;
            return true;
        }

        // Swings back to the player's view; returned runs once the camera is the player's again (at once if it
        // never left).
        public void Return(Action returned)
        {
            if (!_isActive)
            {
                returned?.Invoke();
                return;
            }

            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _returnSeconds);
            _playerView.Prioritize();
            _isFocused = false;
            _arrived = null;
            _returned = returned;
        }

        // Seen from the player's side of the tower, far enough back for the whole height to fit the frame, then slid
        // sideways so the lighthouse stands at _screenX without turning the view.
        private void PlaceTowerView(Transform tower, Vector3 from, float fieldOfView, float aspect)
        {
            Vector3 aim = tower.position + Vector3.up * (_lighthouseHeight * _aimHeight);
            Vector3 side = from - tower.position;
            side.y = 0f;

            if (side.sqrMagnitude < 0.01f)
            {
                side = -tower.forward;
            }

            float halfHeight = _lighthouseHeight * 0.5f * _framePadding;
            float tanHalf = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = halfHeight / tanHalf;
            // At this distance the screen's half width spans distance * tanHalf * aspect.
            float slide = (1f - 2f * _screenX) * distance * tanHalf * aspect;
            Quaternion rotation = Quaternion.LookRotation(-side.normalized, Vector3.up) * Quaternion.Euler(_elevation, 0f, 0f);
            Vector3 position = aim - rotation * Vector3.forward * distance + rotation * Vector3.right * slide;
            _towerView.transform.SetPositionAndRotation(position, rotation);
        }

        private void Finish()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _isFocused = false;
            _arrived = null;
            _playerView.enabled = false;
            _towerView.enabled = false;

            if (_brain)
            {
                _brain.enabled = false;
            }

            _brain = null;
            _output = null;
            Action returned = _returned;
            _returned = null;
            returned?.Invoke();
        }
    }
}
