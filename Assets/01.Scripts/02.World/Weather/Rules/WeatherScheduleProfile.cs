using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    [CreateAssetMenu(fileName = "WeatherScheduleProfile", menuName = "TheLightHouse/World/WeatherScheduleProfile")]
    public class WeatherScheduleProfile : ScriptableObject
    {
        private const float MinRatio = 0.01f;
        private const float MaxRatio = 1f;

        [SerializeField] private WeatherType _firstDayType = WeatherType.Calm;
        [SerializeField, Range(MinRatio, MaxRatio)] private float _transitionDayRatio = 0.2f;
        [SerializeField]
        private WeatherWeight[] _weights =
        {
            new WeatherWeight { Type = WeatherType.Calm, Weight = 35f },
            new WeatherWeight { Type = WeatherType.Overcast, Weight = 25f },
            new WeatherWeight { Type = WeatherType.Rain, Weight = 25f },
            new WeatherWeight { Type = WeatherType.Storm, Weight = 15f }
        };

        public WeatherType FirstDayType => _firstDayType;
        public float TransitionDayRatio => _transitionDayRatio;

        public WeatherWeightTable BuildTable()
        {
            return new WeatherWeightTable(_weights);
        }

        public float GetTransitionSeconds(float daySeconds)
        {
            return Mathf.Max(0f, daySeconds) * _transitionDayRatio;
        }

        private void OnValidate()
        {
            _transitionDayRatio = Mathf.Clamp(_transitionDayRatio, MinRatio, MaxRatio);

            if (_firstDayType == WeatherType.Maelstrom)
            {
                _firstDayType = WeatherType.Calm;
            }
        }
    }
}
