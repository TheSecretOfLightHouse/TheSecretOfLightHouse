using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public class SlamReporter
    {
        private float _previousDepth;
        private bool _hasPrevious;
        private double _lastReportTime = double.NegativeInfinity;

        public float Threshold { get; set; }

        public float Cooldown { get; set; }

        public void Reset()
        {
            _hasPrevious = false;
            _lastReportTime = double.NegativeInfinity;
        }

        public void Track(float meanDepth, float deltaTime, double time, Vector3 position, uint shipId)
        {
            float previousDepth = _hasPrevious ? _previousDepth : meanDepth;
            _previousDepth = meanDepth;
            _hasPrevious = true;

            float strength = SlamEstimator.Estimate(previousDepth, meanDepth, deltaTime);
            if (strength < Threshold || time - _lastReportTime < Cooldown)
            {
                return;
            }

            if (!WorldServices.TryGet(out IHazardEvents events))
            {
                return;
            }

            _lastReportTime = time;
            events.Raise(new HazardHit(HazardType.WaveSlam, position, strength, shipId));
        }
    }
}
