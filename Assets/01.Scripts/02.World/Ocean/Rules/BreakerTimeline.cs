namespace Lighthouse.World.Ocean.Rules
{
    public static class BreakerTimeline
    {
        public static BreakerPhase Evaluate(in BreakerWaveTuning tuning, float age, out float progress)
        {
            progress = 0f;
            if (age < 0f || age >= tuning.Duration)
            {
                return BreakerPhase.None;
            }

            if (age < tuning.ApproachEnd)
            {
                progress = age / tuning.ApproachEnd;
                return BreakerPhase.Approach;
            }

            if (age < tuning.CurlEnd)
            {
                progress = (age - tuning.ApproachEnd) / (tuning.CurlEnd - tuning.ApproachEnd);
                return BreakerPhase.Curl;
            }

            if (age < tuning.ImpactEnd)
            {
                progress = (age - tuning.CurlEnd) / (tuning.ImpactEnd - tuning.CurlEnd);
                return BreakerPhase.Impact;
            }

            progress = (age - tuning.ImpactEnd) / (tuning.Duration - tuning.ImpactEnd);
            return BreakerPhase.Rundown;
        }
    }
}
