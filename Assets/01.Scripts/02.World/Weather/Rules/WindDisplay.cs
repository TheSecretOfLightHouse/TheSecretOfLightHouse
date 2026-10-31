namespace Lighthouse.World.Weather.Rules
{
    public readonly struct WindDisplay
    {
        public readonly float Speed;
        public readonly float AngleDegrees;
        public readonly CompassPoint Compass;
        public readonly WindStrength Strength;

        public WindDisplay(float speed, float angleDegrees, CompassPoint compass, WindStrength strength)
        {
            Speed = speed;
            AngleDegrees = angleDegrees;
            Compass = compass;
            Strength = strength;
        }

        public bool HasWind => Strength != WindStrength.Still;
    }
}
