using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.WorldState.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.WorldState.Net
{
    [DisallowMultipleComponent]
    public class WorldStateNet : NetworkBehaviour
    {
        [SerializeField] private GameClock _gameClock;
        [SerializeField] private bool _isBeginOnStartServer = true;

        [SyncVar(hook = nameof(OnMatchChanged))]
        private MatchStartInfo _match;

        private MatchClockBinder _binder;
        private LighthouseNetworkManager _manager;

        public bool IsMatchStarted => _match.IsStarted;
        public bool IsClockReady => _binder != null && _binder.IsReady;

        private void Awake()
        {
            if (_gameClock == null)
            {
                Debug.LogError($"[{nameof(WorldStateNet)}] GameClock is not assigned.");
                return;
            }

            _binder = new MatchClockBinder(_gameClock, new NetworkClock());
        }

        private void Update()
        {
            if (_binder != null)
            {
                _binder.Tick();
            }
        }

        public override void OnStartServer()
        {
            if (_binder == null)
            {
                return;
            }

            _binder.Attach();

            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;

            if (manager != null && manager.IsSessionFlowEnabled)
            {
                _manager = manager;
                _manager.MatchStarted += HandleMatchStarted;
                return;
            }

            if (_isBeginOnStartServer)
            {
                BeginMatch(NetworkTime.time);
            }
        }

        public override void OnStartClient()
        {
            if (_binder == null)
            {
                return;
            }

            _binder.Attach();
            _binder.Submit(_match, isServer);
        }

        public override void OnStopServer()
        {
            if (_manager != null)
            {
                _manager.MatchStarted -= HandleMatchStarted;
                _manager = null;
            }

            _match = default;
            ReleaseClock();
        }

        public override void OnStopClient()
        {
            if (isServer)
            {
                return;
            }

            ReleaseClock();
        }

        [Server]
        public void BeginMatch(double startTime)
        {
            if (_binder == null || _match.IsStarted)
            {
                return;
            }

            int totalDays = _gameClock.TotalDays;

            if (totalDays <= 0 || totalDays > byte.MaxValue)
            {
                Debug.LogError($"[{nameof(WorldStateNet)}] TotalDays {totalDays} cannot be sent.");
                return;
            }

            var info = new MatchStartInfo
            {
                TotalDays = (byte)totalDays,
                StartTime = startTime
            };

            _match = info;

            if (!NetworkServer.activeHost)
            {
                _binder.Submit(info, true);
            }
        }

        private void HandleMatchStarted(uint matchId, double startTime, IReadOnlyList<uint> roster)
        {
            BeginMatch(startTime);
        }

        private void OnMatchChanged(MatchStartInfo oldValue, MatchStartInfo newValue)
        {
            if (_binder != null)
            {
                _binder.Submit(newValue, isServer);
            }
        }

        private void ReleaseClock()
        {
            if (_binder != null)
            {
                _binder.Detach();
            }
        }
    }
}
