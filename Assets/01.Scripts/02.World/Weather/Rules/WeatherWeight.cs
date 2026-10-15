using System;

namespace Lighthouse.World.Weather.Rules
{
    [Serializable]
    public struct WeatherWeight
    {
        public WeatherType Type;
        public float Weight;
    }
}
