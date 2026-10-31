using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public static class WindDisplayModel
    {
        public const float StillLimit = 0.1f;
        public const float LightLimit = 5f;
        public const float ModerateLimit = 10f;
        public const float StrongLimit = 18f;

        private const float FullTurn = 360f;
        private const float CompassStep = 45f;
        private const int CompassCount = 8;

        public static WindDisplay Evaluate(Vector2 wind)
        {
            float speed = wind.magnitude;

            if (speed < StillLimit)
            {
                return new WindDisplay(0f, 0f, CompassPoint.North, WindStrength.Still);
            }

            float angle = Mathf.Atan2(wind.x, wind.y) * Mathf.Rad2Deg;

            if (angle < 0f)
            {
                angle += FullTurn;
            }

            int index = Mathf.RoundToInt(angle / CompassStep) % CompassCount;
            return new WindDisplay(speed, angle, (CompassPoint)index, GetStrength(speed));
        }

        private static WindStrength GetStrength(float speed)
        {
            if (speed < LightLimit)
            {
                return WindStrength.Light;
            }

            if (speed < ModerateLimit)
            {
                return WindStrength.Moderate;
            }

            return speed < StrongLimit ? WindStrength.Strong : WindStrength.Violent;
        }
    }
}
