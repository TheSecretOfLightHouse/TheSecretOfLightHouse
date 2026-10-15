using System;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public static class WeatherForecastGenerator
    {
        public static IForecast[] Generate(ulong seed, int totalDays, WeatherType firstDay, WeatherWeightTable table)
        {
            if (totalDays <= 0)
            {
                return Array.Empty<IForecast>();
            }

            WeatherType first = firstDay == WeatherType.Maelstrom ? WeatherType.Calm : firstDay;
            bool isTableUsable = table != null && !table.IsEmpty;
            var rng = new SeededRandom(seed);
            var days = new IForecast[totalDays];

            for (int i = 0; i < totalDays; i++)
            {
                WeatherType type = first;

                if (i > 0 && isTableUsable)
                {
                    type = table.Pick(rng.NextFloat());
                }

                days[i] = new DailyForecast(i + 1, type);
            }

            return days;
        }
    }
}
