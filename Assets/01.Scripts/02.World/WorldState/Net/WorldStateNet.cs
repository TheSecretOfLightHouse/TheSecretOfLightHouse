using Lighthouse.World.Clock.Rules;
using Lighthouse.World.WorldState.Rules;
using Lighthouse.World.WorldState.Server;
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

        public bool IsMatchStarted => _match.IsStarted;
        public int MapSeed => _match.MapSeed;

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

            if (_isBeginOnStartServer)
            {
                BeginMatch();
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
        public void BeginMatch()
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
                MapSeed = MatchSeedSource.Next(),
                TotalDays = (byte)totalDays,
                StartTime = NetworkTime.time
            };

            _match = info;

            if (!NetworkServer.activeHost)
            {
                _binder.Submit(info, true);
            }
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
