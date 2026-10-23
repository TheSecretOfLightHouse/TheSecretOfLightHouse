using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class BuoyancySampleSession
    {
        private readonly Rigidbody _body;
        private readonly Vector3[] _localPoints;
        private readonly Vector3[] _worldPoints;
        private readonly float[] _waterHeights;

        private WaterSamplerHost _host;
        private IWaterSampleSource _source;
        private SamplerTicket _ticket;
        private ulong _submittedVersion;
        private double _submittedFixedTime;
        private bool _isAllocated;
        private bool _hasSubmitted;

        public BuoyancySampleSession(
            Rigidbody body,
            IReadOnlyList<Vector3> localPoints)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (localPoints == null || localPoints.Count == 0)
            {
                throw new ArgumentException(
                    "At least one buoyancy point is required.",
                    nameof(localPoints));
            }

            _body = body;

            int count = localPoints.Count;

            _localPoints = new Vector3[count];
            _worldPoints = new Vector3[count];
            _waterHeights = new float[count];

            for (int i = 0; i < count; i++)
            {
                _localPoints[i] = localPoints[i];
            }
        }

        public bool TryOpen(
            WaterSamplerHost host,
            SamplerPool pool,
            IWaterSampleSource source)
        {
            if (_isAllocated
                || _body == null
                || host == null
                || !host.isActiveAndEnabled
                || source == null)
            {
                return false;
            }

            if (!host.TryAllocate(
                    pool,
                    _localPoints.Length,
                    out SamplerTicket ticket))
            {
                return false;
            }

            _host = host;
            _source = source;
            _ticket = ticket;
            _isAllocated = true;

            Invalidate();
            _host.RegisterSource(_source);

            return true;
        }

        public void Close()
        {
            bool wasAllocated = _isAllocated;
            WaterSamplerHost host = _host;
            IWaterSampleSource source = _source;
            SamplerTicket ticket = _ticket;

            _isAllocated = false;
            _host = null;
            _source = null;
            _ticket = default;

            Invalidate();

            if (!wasAllocated || host == null)
            {
                return;
            }

            host.UnregisterSource(source);
            host.Release(in ticket);
        }

        public void Invalidate()
        {
            _hasSubmitted = false;
        }

        public void SubmitPoints(bool canSimulate)
        {
            Invalidate();

            if (!_isAllocated
                || !canSimulate
                || _body == null
                || _body.isKinematic
                || _host == null
                || !_host.isActiveAndEnabled
                || !_host.IsSampling)
            {
                return;
            }

            Transform bodyTransform = _body.transform;

            for (int i = 0; i < _localPoints.Length; i++)
            {
                _worldPoints[i] =
                    bodyTransform.TransformPoint(_localPoints[i]);

                _host.SetPoint(
                    in _ticket,
                    i,
                    _worldPoints[i]);
            }

            _submittedVersion =
                unchecked(_host.CompletedSampleVersion + 1UL);

            _submittedFixedTime = Time.fixedTimeAsDouble;
            _hasSubmitted = true;
        }

        public bool TryGetSamples(
            out ReadOnlySpan<Vector3> worldPoints,
            out ReadOnlySpan<float> waterHeights)
        {
            worldPoints = default;
            waterHeights = default;

            if (!_isAllocated
                || !_hasSubmitted
                || _body == null
                || _body.isKinematic
                || _host == null
                || !_host.isActiveAndEnabled
                || _host.IsSampling
                || _host.CompletedSampleVersion != _submittedVersion
                || _host.CompletedSampleFixedTime != _submittedFixedTime
                || _submittedFixedTime != Time.fixedTimeAsDouble)
            {
                return false;
            }

            for (int i = 0; i < _waterHeights.Length; i++)
            {
                float height = _host.GetHeight(in _ticket, i);

                if (float.IsNaN(height) || float.IsInfinity(height))
                {
                    Invalidate();
                    return false;
                }

                _waterHeights[i] = height;
            }

            worldPoints = _worldPoints;
            waterHeights = _waterHeights;

            return true;
        }
    }
}
