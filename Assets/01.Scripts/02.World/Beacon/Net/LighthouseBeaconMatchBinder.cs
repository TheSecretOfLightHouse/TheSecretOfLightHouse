using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.World.Beacon.View;
using UnityEngine;

namespace Lighthouse.World.Beacon.Net
{
    // Lights the beacon when the match ends in a citizens' win. TowerMatchNet.Ended is server-only, so every machine
    // (the host too) polls the synced Outcome and runs the same path once. A result already there on the first look
    // (late join) lights the beam at once instead of replaying the ignition. Read-only: it never writes to the match.
    [DisallowMultipleComponent]
    public sealed class LighthouseBeaconMatchBinder : MonoBehaviour
    {
        [SerializeField] private LighthouseBeaconView _view;

        [Tooltip("Optional. Left empty, the camera next to the view is used; with none the beam lights without a camera move.")]
        [SerializeField] private BeaconEpilogueCamera _camera;

        private TowerMatchNet _match;
        private bool _hasLooked;
        private bool _isLit;

        private void Awake()
        {
            if (_view == null)
            {
                Debug.LogError($"[{nameof(LighthouseBeaconMatchBinder)}] Beacon view is required.");
                enabled = false;
                return;
            }

            if (_camera == null)
            {
                _view.TryGetComponent(out _camera);
            }
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                _camera.Release();
            }
        }

        private void Update()
        {
            if (_match == null)
            {
                return;
            }

            bool isWin = _match.Outcome == TowerOutcome.CitizensWin;

            if (isWin == _isLit && _hasLooked)
            {
                return;
            }

            if (isWin)
            {
                bool isInstant = !_hasLooked;
                // The camera is best effort: if something else drives it, the beam still lights.
                float delay = _camera != null && _camera.TryFocus(_view.transform, isInstant) && !isInstant
                    ? _camera.BlendSeconds
                    : 0f;
                _view.TurnOnAfter(delay, isInstant);
            }
            else
            {
                if (_camera != null)
                {
                    _camera.Release();
                }

                _view.TurnOff();
            }

            _isLit = isWin;
            _hasLooked = true;
        }

        // Pass null to unbind. A new match starts dark.
        public void Bind(TowerMatchNet match)
        {
            _match = match;
            _hasLooked = false;
            _isLit = false;

            if (_camera != null)
            {
                _camera.Release();
            }

            if (_view != null)
            {
                _view.TurnOff();
            }
        }
    }
}
