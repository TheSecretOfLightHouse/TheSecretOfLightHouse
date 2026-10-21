using System.Collections.Generic;

namespace Lighthouse.Stations.View
{
    // Every station screen opens with E (2026-10-21) and their ranges can overlap, so only the nearest station in
    // range shows its prompt and takes the key. Each screen asks on its own, so Update order does not matter.
    public static class StationScreenFocus
    {
        private static readonly List<IStationScreen> Screens = new List<IStationScreen>();

        public static void Register(IStationScreen screen)
        {
            if (!Screens.Contains(screen))
            {
                Screens.Add(screen);
            }
        }

        public static void Unregister(IStationScreen screen)
        {
            Screens.Remove(screen);
        }

        // Ties go to the screen registered first, so two stations at the same distance never both answer.
        public static bool IsNearest(IStationScreen screen)
        {
            if (!screen.TryGetLocalSqrDistance(out float own))
            {
                return false;
            }

            bool isBefore = true;

            for (int i = 0; i < Screens.Count; i++)
            {
                IStationScreen other = Screens[i];

                if (other == screen)
                {
                    isBefore = false;
                    continue;
                }

                if (other.TryGetLocalSqrDistance(out float distance) && (distance < own || isBefore && distance == own))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
