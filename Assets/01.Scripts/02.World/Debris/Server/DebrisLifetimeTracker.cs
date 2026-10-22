using System;
using System.Collections.Generic;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Debris.Rules;

namespace Lighthouse.World.Debris.Server
{
    public sealed class DebrisLifetimeTracker : IDebrisTracker, IDisposable
    {
        private readonly IClock _clock;
        private readonly DebrisConfig _config;
        private readonly DebrisExpiryList _list = new DebrisExpiryList();
        private readonly List<DebrisExpiryEntry> _expired = new List<DebrisExpiryEntry>();
        private readonly Action<IExpirableDrop> _releasedHandler;

        public DebrisLifetimeTracker(IClock clock, DebrisConfig config)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _releasedHandler = HandleReleased;
        }

        public int Count => _list.Count;

        public bool Register(IExpirableDrop drop, DebrisKind kind)
        {
            if (drop == null)
            {
                return false;
            }

            if (!_config.TryGetLifetime(kind, out float seconds))
            {
                return true;
            }

            if (_list.Remove(drop))
            {
                drop.Released -= _releasedHandler;
            }

            drop.Released += _releasedHandler;
            _list.Add(drop, drop.Generation, _clock.Now + seconds);
            return true;
        }

        public void Tick()
        {
            _expired.Clear();
            if (_list.CollectExpired(_clock.Now, _expired) == 0)
            {
                return;
            }

            for (int i = 0; i < _expired.Count; i++)
            {
                DebrisExpiryEntry entry = _expired[i];
                entry.Drop.Released -= _releasedHandler;
                entry.Drop.TryExpire(entry.Generation);
            }

            _expired.Clear();
        }

        public void Dispose()
        {
            _expired.Clear();
            _list.Drain(_expired);
            for (int i = 0; i < _expired.Count; i++)
            {
                _expired[i].Drop.Released -= _releasedHandler;
            }

            _expired.Clear();
        }

        private void HandleReleased(IExpirableDrop drop)
        {
            drop.Released -= _releasedHandler;
            _list.Remove(drop);
        }
    }
}
