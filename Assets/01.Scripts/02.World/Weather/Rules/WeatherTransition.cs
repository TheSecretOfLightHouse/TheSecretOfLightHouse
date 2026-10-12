using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public readonly struct WeatherTransition
    {
        public WeatherTransition(WeatherState from, WeatherState to, double startTime, float duration)
        {
            From = from;
            To = to;
            StartTime = startTime;
            Duration = duration;
        }

        public WeatherState From { get; }

        public WeatherState To { get; }

        public double StartTime { get; }

        public float Duration { get; }

        public float Evaluate(double now)
        {
            if (Duration <= 0f)
            {
                return now >= StartTime ? 1f : 0f;
            }

            float t = Mathf.Clamp01((float)((now - StartTime) / Duration));
            return t * t * (3f - 2f * t);
        }

        public bool IsCompleteAt(double now)
        {
            return now >= StartTime + Duration;
        }
    }
}
