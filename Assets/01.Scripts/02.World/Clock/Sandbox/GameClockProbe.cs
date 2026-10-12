using Lighthouse.World.Clock.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.Sandbox
{
    public class GameClockProbe : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private double _resyncElapsed = 13.0;

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
            return Time.timeAsDouble - _clock.GameStartTime;
        }
    }
}
