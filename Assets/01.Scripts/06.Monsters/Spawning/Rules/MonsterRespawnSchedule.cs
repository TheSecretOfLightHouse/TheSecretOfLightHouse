using System;
using System.Collections.Generic;

namespace Lighthouse.Monsters.Spawning.Rules
{
    internal sealed class MonsterRespawnSchedule
    {
        private readonly List<double> _deadlines;
        private double _activeTime;
        private double _lastTime;

        internal bool IsRunning { get; private set; }
        internal int Count => _deadlines.Count;

        internal MonsterRespawnSchedule(int capacity, bool isRunning, double now)
        {
            _deadlines = new List<double>(capacity);
            IsRunning = isRunning;
            _lastTime = now;
        }

        internal void Advance(double now)
        {
            if (IsRunning)
            {
                _activeTime += Math.Max(0d, now - _lastTime);
            }

            _lastTime = Math.Max(_lastTime, now);
        }

        internal void SetRunning(bool isRunning, double now)
        {
            Advance(now);
            IsRunning = isRunning;
        }

        internal void Schedule(double now, double delay)
        {
            Advance(now);
            _deadlines.Add(_activeTime + delay);
        }

        internal int ConsumeReady()
        {
            if (!IsRunning)
            {
                return 0;
            }

            int ready = 0;
            for (int i = _deadlines.Count - 1; i >= 0; i--)
            {
                if (_deadlines[i] <= _activeTime)
                {
                    _deadlines.RemoveAt(i);
                    ready++;
                }
            }

            return ready;
        }

        internal double GetRemaining(int index)
        {
            return Math.Max(0d, _deadlines[index] - _activeTime);
        }

        internal void Clear()
        {
            _deadlines.Clear();
        }
    }
}
