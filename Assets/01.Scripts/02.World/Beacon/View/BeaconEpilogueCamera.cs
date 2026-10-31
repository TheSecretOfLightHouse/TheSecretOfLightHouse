using Unity.Cinemachine;
using UnityEngine;

namespace Lighthouse.World.Beacon.View
{
    // Camera move for the citizens' ending: from the player's view the main camera swings out to a low wide shot of
    // the lighthouse and holds, so the beam sweeping the calming sea is what the result screen sits over. Same pattern
    // as the lighthouse screen's focus camera: Cinemachine drives the camera only while the shot is held, the brain
    // stays disabled otherwise and PlayerCamera keeps owning it. Runs after PlayerCamera so its pose this frame is the
    // start of the move. The two virtual cameras are made at run time, so there is nothing else to wire.
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class BeaconEpilogueCamera : MonoBehaviour
    {
        [Header("Shot")]
        [Tooltip("How far the camera stands from the lantern, seen from the player's side.")]
        [SerializeField, Min(1f)] private float _distance = 34f;
        [Tooltip("Camera height relative to the lantern. Negative stands lower, so the tower rises and the beam crosses the sky.")]
        [SerializeField] private float _heightOffset = -7f;
        [Tooltip("Where the shot aims, relative to the lantern.")]
        [SerializeField] private float _aimHeightOffset = -2f;
        [Tooltip("Where across the screen the lantern stands, 0 left to 1 right. Left of centre leaves room for the result screen.")]
        [SerializeField, Range(0.1f, 0.9f)] private float _screenX = 0.32f;

        [Header("Blend")]
        [SerializeField, Min(0f)] private float _blendSeconds = 1.8f;

        private CinemachineCamera _playerView;
        private CinemachineCamera _shotView;
        private CinemachineBrain _brain;
        private Transform _output;
        private bool _isActive;

        public float BlendSeconds => _blendSeconds;
        public bool IsActive => _isActive;

        private void LateUpdate()
        {
            if (!_isActive)
            {
                return;
            }

            if (!_brain || !_output)
            {
                Release();
                return;
            }

            _playerView.transform.SetPositionAndRotation(_output.position, _output.rotation);
            _brain.ManualUpdate();
        }

        private void OnDisable()
        {
            Release();
        }

        // False when the camera is already driven by something else (the brain is on) or there is no main camera.
        public bool TryFocus(Transform lantern, bool isInstant)
        {
            Camera camera = Camera.main;

            if (_isActive || !isActiveAndEnabled || !lantern || !camera)
            {
                return _isActive;
            }

            _brain = camera.GetComponent<CinemachineBrain>();

            if (!_brain)
            {
                _brain = camera.gameObject.AddComponent<CinemachineBrain>();
            }
            else if (_brain.enabled)
            {
                _brain = null;
                return false;
            }

            EnsureViews();
            _output = camera.transform;
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            _brain.IgnoreTimeScale = true;
            _playerView.Lens = LensSettings.FromCamera(camera);
            _shotView.Lens = _playerView.Lens;
            _playerView.transform.SetPositionAndRotation(_output.position, _output.rotation);
            _playerView.enabled = true;
            _brain.enabled = true;
            // Nothing was live, so this cuts to the player's view: the move below starts from exactly there.
            _brain.ManualUpdate();
            PlaceShot(lantern, _output.position, camera.fieldOfView, camera.aspect);
            CinemachineBlendDefinition.Styles style = isInstant || _blendSeconds <= 0f
                ? CinemachineBlendDefinition.Styles.Cut
                : CinemachineBlendDefinition.Styles.EaseInOut;
            _brain.DefaultBlend = new CinemachineBlendDefinition(style, _blendSeconds);
            _shotView.enabled = true;
            _shotView.Prioritize();
            _isActive = true;
            return true;
        }

        // Hands the camera back at once; the player camera's pose is what the screen shows from the next frame.
        public void Release()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _playerView.enabled = false;
            _shotView.enabled = false;

            if (_brain)
            {
                _brain.enabled = false;
            }

            _brain = null;
            _output = null;
        }

        private void EnsureViews()
        {
            if (!_playerView)
            {
                _playerView = CreateView("EpiloguePlayerView");
                _shotView = CreateView("EpilogueShotView");
            }
        }

        private CinemachineCamera CreateView(string viewName)
        {
            var go = new GameObject(viewName);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<CinemachineCamera>();
            view.enabled = false;
            return view;
        }

        // Seen from the player's side of the lantern, then slid sideways so it stands at _screenX without turning the view.
        private void PlaceShot(Transform lantern, Vector3 from, float fieldOfView, float aspect)
        {
            Vector3 side = from - lantern.position;
            side.y = 0f;

            if (side.sqrMagnitude < 0.01f)
            {
                side = Vector3.back;
            }

            side.Normalize();
            Vector3 aim = lantern.position + Vector3.up * _aimHeightOffset;
            Vector3 position = lantern.position + side * _distance + Vector3.up * _heightOffset;
            Quaternion rotation = Quaternion.LookRotation(aim - position, Vector3.up);
            float tanHalf = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float slide = (1f - 2f * _screenX) * _distance * tanHalf * aspect;
            _shotView.transform.SetPositionAndRotation(position + rotation * Vector3.right * slide, rotation);
        }
    }
}
