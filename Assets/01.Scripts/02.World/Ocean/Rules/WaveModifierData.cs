using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct WaveModifierData
    {
        public readonly WaveModifierKind Kind;
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Strength;
        public readonly float StartTime;
        public readonly float BandWidth;
        public readonly float Wavelength;
        public readonly float Speed;

        public WaveModifierData(WaveModifierKind kind, Vector2 center,
            float radius, float strength, float startTime,
            float bandWidth = 0f, float wavelength = 0f, float speed = 0f)
        {
            Kind = kind;
            Center = center;
            Radius = radius;
            Strength = strength;
            StartTime = startTime;
            BandWidth = bandWidth;
            Wavelength = wavelength;
            Speed = speed;
        }
    }
}
