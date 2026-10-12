using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public class WeatherWindSource : IWindSource, IWeatherListener
    {
        private Vector2 _wind;

        public Vector2 GetWind(double time)
        {
            return _wind;
        }

        public void HandleWeatherChanged(in WeatherSnapshot snapshot)
        {
            _wind = snapshot.Wind;
        }
    }
}
