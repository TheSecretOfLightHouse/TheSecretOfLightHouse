using System;

namespace Lighthouse.World.Weather.Rules
{
    public static class WeatherStateSet
    {
        private static readonly Entry[] Entries =
        {
            new Entry(WeatherType.Calm, target => new CalmState(target)),
            new Entry(WeatherType.Overcast, target => new OvercastState(target)),
            new Entry(WeatherType.Rain, target => new RainState(target)),
            new Entry(WeatherType.Storm, target => new StormState(target)),
            new Entry(WeatherType.Maelstrom, target => new MaelstromState(target))
        };

        public static bool TryBuild(WeatherCatalog catalog, out WeatherState[] states)
        {
            states = null;

            if (catalog == null || !catalog.Validate(out _))
            {
                return false;
            }

            var built = new WeatherState[GetLength()];

            for (int i = 0; i < Entries.Length; i++)
            {
                if (!catalog.TryGetProfile(Entries[i].Type, out WeatherProfile profile)
                    || !WeatherSnapshot.TryCreate(catalog.Layout, profile, out WeatherSnapshot target))
                {
                    return false;
                }

                built[(int)Entries[i].Type] = Entries[i].Create(target);
            }

            states = built;
            return true;
        }

        private static int GetLength()
        {
            int max = 0;

            for (int i = 0; i < Entries.Length; i++)
            {
                max = Math.Max(max, (int)Entries[i].Type);
            }

            return max + 1;
        }

        private readonly struct Entry
        {
            public Entry(WeatherType type, Func<WeatherSnapshot, WeatherState> create)
            {
                Type = type;
                Create = create;
            }

            public WeatherType Type { get; }

            public Func<WeatherSnapshot, WeatherState> Create { get; }
        }
    }
}
