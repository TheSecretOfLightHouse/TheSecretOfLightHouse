using Lighthouse.World.Clock.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.Sandbox
{
    public class GameClockProbe : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private double _resyncElapsed = 13.0;
        [SerializeField] private double _advanceSeconds = 30.0;

        private ManualClock _manual;

        private void OnEnable()
        {
            if (_clock == null)
            {
                Debug.LogError($"[{nameof(GameClockProbe)}] GameClock is not assigned.");
                return;
            }

            _clock.DayStarted += HandleDayStarted;
            _clock.NightStarted += HandleNightStarted;
            _clock.ReturnWarningIssued += HandleReturnWarningIssued;
        }

        private void OnDisable()
        {
            if (_clock == null)
            {
                return;
            }

            _clock.DayStarted -= HandleDayStarted;
            _clock.NightStarted -= HandleNightStarted;
            _clock.ReturnWarningIssued -= HandleReturnWarningIssued;
        }

        [ContextMenu("Clock/Log State")]
        private void LogState()
        {
            Debug.Log($"[{nameof(GameClockProbe)}] State day={_clock.DayIndex}/{_clock.TotalDays} phase={_clock.Phase} elapsed={Elapsed():F1}");
        }

        [ContextMenu("Clock/Resync Silently")]
        private void ResyncSilently()
        {
            _clock.Resync(Time.timeAsDouble - _resyncElapsed);
            Debug.Log($"[{nameof(GameClockProbe)}] Resync to elapsed={_resyncElapsed} -> day={_clock.DayIndex} phase={_clock.Phase}");
        }

        [ContextMenu("Clock/Manual Source Begin At")]
        private void ManualSourceBeginAt()
        {
            _manual = new ManualClock { Now = 100.0 };
            _clock.UseSource(_manual);
            _clock.BeginAt(_manual.Now);
            Debug.Log($"[{nameof(GameClockProbe)}] Manual source, BeginAt now={_manual.Now} -> day={_clock.DayIndex} phase={_clock.Phase}");
        }

        [ContextMenu("Clock/Manual Advance")]
        private void ManualAdvance()
        {
            if (_manual == null)
            {
                Debug.LogWarning($"[{nameof(GameClockProbe)}] Run 'Manual Source Begin At' first.");
                return;
            }

            _manual.Now += _advanceSeconds;
            Debug.Log($"[{nameof(GameClockProbe)}] Manual advance now={_manual.Now} elapsed={_manual.Now - _clock.GameStartTime:F1}");
        }

        [ContextMenu("Clock/Manual Resync Silently")]
        private void ManualResyncSilently()
        {
            if (_manual == null)
            {
                Debug.LogWarning($"[{nameof(GameClockProbe)}] Run 'Manual Source Begin At' first.");
                return;
            }

            _clock.Resync(_manual.Now - _resyncElapsed);
            Debug.Log($"[{nameof(GameClockProbe)}] Manual resync elapsed={_resyncElapsed} -> day={_clock.DayIndex} phase={_clock.Phase}");
        }

        private void HandleDayStarted(int day)
        {
            Debug.Log($"[{nameof(GameClockProbe)}] DayStarted day={day} t={Elapsed():F1}");
        }

        private void HandleNightStarted(int day)
        {
            Debug.Log($"[{nameof(GameClockProbe)}] NightStarted day={day} t={Elapsed():F1}");
        }

        private void HandleReturnWarningIssued(int day)
        {
            Debug.Log($"[{nameof(GameClockProbe)}] ReturnWarningIssued day={day} t={Elapsed():F1}");
        }

        private double Elapsed()
        {
            double now = _manual != null ? _manual.Now : Time.timeAsDouble;
            return now - _clock.GameStartTime;
        }
    }
}
