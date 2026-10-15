using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public sealed class WeatherWeightTable
    {
        private readonly WeatherType[] _types;
        private readonly float[] _cumulative;
        private readonly float _total;

        public WeatherWeightTable(IReadOnlyList<WeatherWeight> weights)
        {
            var types = new List<WeatherType>();
            var cumulative = new List<float>();
            float total = 0f;

            if (weights != null)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    WeatherWeight weight = weights[i];

                    if (weight.Type == WeatherType.Maelstrom || !(weight.Weight > 0f))
                    {
                        continue;
                    }

                    total += weight.Weight;
                    types.Add(weight.Type);
                    cumulative.Add(total);
                }
            }

            _types = types.ToArray();
            _cumulative = cumulative.ToArray();
            _total = total;
        }

        public bool IsEmpty => _types.Length == 0;

        public WeatherType Pick(float roll)
        {
            if (IsEmpty)
            {
                return WeatherType.Calm;
            }

            float target = Mathf.Clamp01(roll) * _total;

            for (int i = 0; i < _cumulative.Length; i++)
            {
                if (target < _cumulative[i])
                {
                    return _types[i];
                }
            }

            return _types[_types.Length - 1];
        }
    }
}
