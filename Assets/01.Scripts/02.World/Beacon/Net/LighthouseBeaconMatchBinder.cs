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

        private TowerMatchNet _match;
        private bool _hasLooked;
        private bool _isLit;

        private void Awake()
        {
            if (_view == null)
            {
                Debug.LogError($"[{nameof(LighthouseBeaconMatchBinder)}] Beacon view is required.");
                enabled = false;
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
                _view.TurnOn(!_hasLooked);
            }
            else
            {
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

            if (_view != null)
            {
                _view.TurnOff();
            }
        }
    }
}
