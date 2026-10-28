using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [CreateAssetMenu(
        fileName = "WeatherEnvironmentProfile",
        menuName = "TheLightHouse/World/Environment/Weather")]
    public sealed class WeatherEnvironmentProfile : ScriptableObject
    {
        [SerializeField] private WeatherType _type = WeatherType.Calm;

        [SerializeField]
        private WeatherEnvironmentState _appearance =
            new WeatherEnvironmentState(
                Color.white, 0f, 1f, 1f, 1f, 0f);

        [SerializeField]
        private SkyEnvironmentState _abyssSky = new SkyEnvironmentState(
            new Color(0.003f, 0.004f, 0.012f),
            new Color(0.015f, 0.02f, 0.045f),
            new Color(0.34f, 0.22f, 0.65f),
            0.15f, 0.8f, 0.04f, 0.75f);

        public WeatherType Type => _type;
        public WeatherEnvironmentState Appearance => _appearance;
        public SkyEnvironmentState AbyssSky => _abyssSky;
    }
}
