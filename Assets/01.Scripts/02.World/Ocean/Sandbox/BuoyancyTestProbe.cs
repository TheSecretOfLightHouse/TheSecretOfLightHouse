using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class BuoyancyTestProbe : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _settleSeconds = 15f;
        [SerializeField, Min(1f)] private float _windowSeconds = 10f;

        private readonly List<Entry> _entries = new List<Entry>();

        private float _elapsed;
        private float _windowElapsed;
        private long _windowStartBytes;
        private bool _isWindowOpen;

        public void Track(BuoyantObject target, float expectedFraction)
        {
            if (target != null && target.TryGetComponent(out Rigidbody body))
            {
                _entries.Add(new Entry(target, body, expectedFraction));
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _elapsed = 0f;
            _isWindowOpen = false;
        }

        private void FixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;

            if (_elapsed < _settleSeconds)
            {
                return;
            }

            if (!_isWindowOpen)
            {
                ResetWindow();
                _isWindowOpen = true;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Sample();
            }

            _windowElapsed += Time.fixedDeltaTime;

            if (_windowElapsed >= _windowSeconds)
            {
                Report();
                ResetWindow();
            }
        }

        [ContextMenu("Report Now")]
        public void Report()
        {
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - _windowStartBytes;

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Log();
            }

            Debug.Log(
                $"[{nameof(BuoyancyTestProbe)}] window={_windowElapsed:F1}s bodies={_entries.Count} mainThreadAlloc={allocated}B (all scripts)");
        }

        private void ResetWindow()
        {
            _windowElapsed = 0f;
            _windowStartBytes = System.GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Reset();
            }
        }

        private sealed class Entry
        {
            private readonly BuoyantObject _target;
            private readonly Rigidbody _body;
            private readonly float _expected;

            private double _fractionSum;
            private int _steps;
            private int _validSteps;
            private int _noLeaseSteps;
            private float _minY;
            private float _maxY;
            private float _maxTilt;

            public Entry(BuoyantObject target, Rigidbody body, float expected)
            {
                _target = target;
                _body = body;
                _expected = expected;
                Reset();
            }

            public void Reset()
            {
                _fractionSum = 0.0;
                _steps = 0;
                _validSteps = 0;
                _noLeaseSteps = 0;
                _minY = float.MaxValue;
                _maxY = float.MinValue;
                _maxTilt = 0f;
            }

            public void Sample()
            {
                if (_target == null)
                {
                    return;
                }

                _steps++;

                BuoyancyReading reading = _target.Reading;

                if (reading.IsValid)
                {
                    _validSteps++;
                    _fractionSum += reading.SubmergedFraction;
                }

                if (!_target.HasLease)
                {
                    _noLeaseSteps++;
                }

                float y = _body.position.y;

                _minY = Mathf.Min(_minY, y);
                _maxY = Mathf.Max(_maxY, y);
                _maxTilt = Mathf.Max(
                    _maxTilt,
                    Vector3.Angle(Vector3.up, _body.transform.up));
            }

            public void Log()
            {
                if (_target == null || _steps == 0)
                {
                    return;
                }

                float average = _validSteps > 0 ? (float)(_fractionSum / _validSteps) : float.NaN;
                string expected = float.IsNaN(_expected) ? "n/a" : _expected.ToString("F3");

                Debug.Log(
                    $"[{nameof(BuoyancyTestProbe)}] {_target.name} expect={expected} fraction={average:F3} "
                    + $"valid={100f * _validSteps / _steps:F0}% heave={_maxY - _minY:F2}m "
                    + $"tilt(max)={_maxTilt:F1}deg y={_body.position.y:F2} noLease={_noLeaseSteps}");
            }
        }
    }
}
