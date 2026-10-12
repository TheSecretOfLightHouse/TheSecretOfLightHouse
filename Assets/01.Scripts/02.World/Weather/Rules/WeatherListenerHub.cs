using System.Collections.Generic;

namespace Lighthouse.World.Weather.Rules
{
    public class WeatherListenerHub
    {
        private readonly List<IWeatherListener> _listeners = new List<IWeatherListener>();

        public bool Add(IWeatherListener listener)
        {
            if (listener == null || _listeners.Contains(listener))
            {
                return false;
            }

            _listeners.Add(listener);
            return true;
        }

        public bool Remove(IWeatherListener listener)
        {
            return _listeners.Remove(listener);
        }

        public void Notify(in WeatherSnapshot snapshot)
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].HandleWeatherChanged(in snapshot);
            }
        }
    }
}
