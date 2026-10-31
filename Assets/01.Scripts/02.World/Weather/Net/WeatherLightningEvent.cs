using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    public struct WeatherLightningEvent
    {
        public uint Id;
        public double Time;
        public Vector3 Position;
        public int Seed;
        public bool IsAbyss;
    }
}
