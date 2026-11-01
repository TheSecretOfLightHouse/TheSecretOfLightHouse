using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Beacon.View
{
    // Lighthouse beam state. Every machine turns the pivot from the same start time and clock, so the sweep needs no
    // sync. The caller (the match, wired in step 2) decides when to light it; this view never reads the match itself.
    [DisallowMultipleComponent]
    public sealed class LighthouseBeaconView : MonoBehaviour
    {
        [SerializeField] private Transform _pivot;
        [SerializeField] private BeaconBeamRig _rig;
        [SerializeField, Min(0.01f)] private float _igniteSeconds = 2f;
        [SerializeField] private float _degreesPerSecond = 30f;
        [SerializeField] private float _startAngle;

        private IClock _clock;
        private BeaconState _state;
        private double _litAt;
        private bool _isInstant;

        public BeaconState State => _state;
        // Extension point for dimming by daylight. Nothing drives it yet (not implemented).
        public float VisibilityScale { get; set; } = 1f;

        private void Awake()
        {
            if (_pivot == null || _rig == null)
            {
                Debug.LogError($"[{nameof(LighthouseBeaconView)}] Pivot and rig are required.");
                enabled = false;
            }
        }

        private void Start()
        {
            WorldServices.TryGet<IClock>(out _clock);
            _rig.Apply(0f);
        }

        private void Update()
        {
            if (_state == BeaconState.Off)
            {
                return;
            }

            double elapsed = Now - _litAt;
            float ignite = _isInstant ? 1f : Mathf.SmoothStep(0f, 1f, (float)(elapsed / _igniteSeconds));

            _state = ignite >= 1f ? BeaconState.On : BeaconState.Igniting;
            _pivot.localRotation = Quaternion.Euler(0f, _startAngle + _degreesPerSecond * (float)elapsed, 0f);
            _rig.Apply(ignite * VisibilityScale);
        }

        private double Now => _clock != null ? _clock.Now : Time.timeAsDouble;

        public void TurnOn(double startTime, bool isInstant)
        {
            _litAt = startTime;
            _isInstant = isInstant;
            _state = BeaconState.Igniting;
        }

        public void TurnOn(bool isInstant)
        {
            TurnOn(Now, isInstant);
        }

        // The ignition starts delaySeconds from now, e.g. when the epilogue camera arrives.
        public void TurnOnAfter(float delaySeconds, bool isInstant)
        {
            TurnOn(Now + Mathf.Max(0f, delaySeconds), isInstant);
        }

        public void TurnOff()
        {
            _state = BeaconState.Off;
            _rig.Apply(0f);
        }

        [ContextMenu("Turn On (Ignite)")]
        private void TurnOnNow()
        {
            TurnOn(Now, false);
        }

        [ContextMenu("Turn On (Instant)")]
        private void TurnOnInstant()
        {
            TurnOn(Now, true);
        }

        [ContextMenu("Turn Off")]
        private void TurnOffNow()
        {
            TurnOff();
        }
    }
}
