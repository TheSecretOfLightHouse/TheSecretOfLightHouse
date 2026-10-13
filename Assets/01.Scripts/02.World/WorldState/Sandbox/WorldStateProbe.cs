using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.WorldState.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.WorldState.Sandbox
{
    public class WorldStateProbe : MonoBehaviour
    {
        [SerializeField] private GameClock _gameClock;
        [SerializeField] private WorldStateNet _worldState;
        [SerializeField] private WaterSamplerHost _sampler;
        [SerializeField] private float _logInterval = 2f;

        private IClock _clock;
        private float _nextLogTime;

        private void OnEnable()
        {
            if (_gameClock == null)
            {
                Debug.LogError($"[{nameof(WorldStateProbe)}] GameClock is not assigned.");
                return;
            }

            _gameClock.DayStarted += HandleDayStarted;
            _gameClock.NightStarted += HandleNightStarted;
            _gameClock.ReturnWarningIssued += HandleReturnWarningIssued;
        }

        private void OnDisable()
        {
            if (_gameClock == null)
            {
                return;
            }

            _gameClock.DayStarted -= HandleDayStarted;
            _gameClock.NightStarted -= HandleNightStarted;
            _gameClock.ReturnWarningIssued -= HandleReturnWarningIssued;
        }

        private void Update()
        {
            if (_logInterval <= 0f || Time.unscaledTime < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.unscaledTime + _logInterval;
            LogState();
        }

        [ContextMenu("WorldState/Log State")]
        private void LogState()
        {
            if (_gameClock == null)
            {
                return;
            }

            string samplerTime = _sampler != null ? _sampler.CurrentTime.ToString("F2") : "-";
            string seed = _worldState != null ? _worldState.MapSeed.ToString() : "-";
            bool isStarted = _worldState != null && _worldState.IsMatchStarted;

            Debug.Log($"[{nameof(WorldStateProbe)}] {Role()} started={isStarted} seed={seed} day={_gameClock.DayIndex}/{_gameClock.TotalDays} phase={_gameClock.Phase} elapsed={Elapsed():F2} samplerTime={samplerTime} netTime={NetworkTime.time:F2} bufferTime={NetworkClient.bufferTime:F3}");
        }

        private void HandleDayStarted(int day)
        {
            Debug.Log($"[{nameof(WorldStateProbe)}] {Role()} DayStarted day={day} elapsed={Elapsed():F2}");
        }

        private void HandleNightStarted(int day)
        {
            Debug.Log($"[{nameof(WorldStateProbe)}] {Role()} NightStarted day={day} elapsed={Elapsed():F2}");
        }

        private void HandleReturnWarningIssued(int day)
        {
            Debug.Log($"[{nameof(WorldStateProbe)}] {Role()} ReturnWarningIssued day={day} elapsed={Elapsed():F2}");
        }

        private double Elapsed()
        {
            if (_clock == null && !WorldServices.TryGet(out _clock))
            {
                return 0.0;
            }

            return _clock.Now - _gameClock.GameStartTime;
        }

        private static string Role()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return "Host";
            }

            if (NetworkServer.active)
            {
                return "Server";
            }

            return NetworkClient.active ? "Client" : "Offline";
        }
    }
}
