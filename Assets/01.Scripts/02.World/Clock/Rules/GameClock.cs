using System;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.Rules
{
    public class GameClock : MonoBehaviour, IGameClock
    {
        [SerializeField] private DayCycleConfig _config;
        [SerializeField] private bool _isStartedOnStart = true;

        private IClock _source;
        private DayCycleState _state = new DayCycleState(1, DayPhase.Day);
        private double _gameStartTime;
        private bool _isRunning;

        public int TotalDays => _config != null ? _config.TotalDays : 0;
        public int DayIndex => _state.DayIndex;
        public DayPhase Phase => _state.Phase;
        public double GameStartTime => _gameStartTime;

        public event Action<int> DayStarted;
        public event Action<int> NightStarted;
        public event Action<int> ReturnWarningIssued;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(GameClock)}] DayCycleConfig is not assigned.");
                enabled = false;
                return;
            }

            _source = new LocalClock();
            WorldServices.Register<IClock>(_source);
            WorldServices.Register<IGameClock>(this);
        }

        private void Start()
        {
            if (_isStartedOnStart)
            {
                Begin();
            }
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            DayCycleState next = Evaluate();
            DayCycleSignals signals = DayCycleTransitions.Detect(in _state, in next);
            _state = next;

            if ((signals & DayCycleSignals.DayStarted) != 0)
            {
                DayStarted?.Invoke(_state.DayIndex);
            }

            if ((signals & DayCycleSignals.ReturnWarningIssued) != 0)
            {
                ReturnWarningIssued?.Invoke(_state.DayIndex);
            }

            if ((signals & DayCycleSignals.NightStarted) != 0)
            {
                NightStarted?.Invoke(_state.DayIndex);
            }
        }

        private void OnDestroy()
        {
            if (_source == null)
            {
                return;
            }

            WorldServices.Unregister<IClock>(_source);
            WorldServices.Unregister<IGameClock>(this);
        }

        public void Begin()
        {
            if (_source == null)
            {
                return;
            }

            Resync(_source.Now);
            DayStarted?.Invoke(_state.DayIndex);
        }

        public void Resync(double gameStartTime)
        {
            if (_source == null)
            {
                return;
            }

            _gameStartTime = gameStartTime;
            _state = Evaluate();
            _isRunning = true;
        }

        private DayCycleState Evaluate()
        {
            return DayCycleMath.Evaluate(
                _source.Now - _gameStartTime,
                _config.DaySeconds,
                _config.NightSeconds,
                _config.ReturnWarningSeconds);
        }
    }
}
