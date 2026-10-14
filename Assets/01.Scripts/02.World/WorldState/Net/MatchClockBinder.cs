using Lighthouse.World.Clock.Rules;
using Lighthouse.World.WorldState.Rules;
using UnityEngine;

namespace Lighthouse.World.WorldState.Net
{
    public sealed class MatchClockBinder
    {
        private readonly GameClock _gameClock;
        private readonly NetworkClock _networkClock;
        private MatchStartInfo _pending;
        private MatchStartInfo _applied;
        private bool _hasPending;
        private bool _isAnnouncing;
        private bool _isAttached;

        public MatchClockBinder(GameClock gameClock, NetworkClock networkClock)
        {
            _gameClock = gameClock;
            _networkClock = networkClock;
        }

        public void Attach()
        {
            _gameClock.UseSource(_networkClock);
            _isAttached = true;
        }

        public bool IsReady => _isAttached && _networkClock.IsReady;

        public void Detach()
        {
            _gameClock.Stop();
            _gameClock.UseSource(new LocalClock());
            _pending = default;
            _applied = default;
            _hasPending = false;
            _isAttached = false;
        }

        public void Submit(MatchStartInfo info, bool isAnnouncing)
        {
            if (!info.IsStarted || info.Equals(_applied))
            {
                return;
            }

            _pending = info;
            _isAnnouncing = isAnnouncing;
            _hasPending = true;
            Tick();
        }

        public void Tick()
        {
            if (!_hasPending || !_isAttached || !_networkClock.IsReady)
            {
                return;
            }

            _hasPending = false;
            _applied = _pending;

            if (_applied.TotalDays != _gameClock.TotalDays)
            {
                Debug.LogWarning($"[{nameof(MatchClockBinder)}] TotalDays differs. server={_applied.TotalDays} local={_gameClock.TotalDays}");
            }

            if (_isAnnouncing)
            {
                _gameClock.BeginAt(_applied.StartTime);
            }
            else
            {
                _gameClock.Resync(_applied.StartTime);
            }
        }
    }
}
