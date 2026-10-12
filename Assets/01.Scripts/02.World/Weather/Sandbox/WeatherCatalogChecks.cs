using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherCatalogChecks
    {
        private const float MidT = 0.5f;

        public static bool RunAll(WeatherCatalog catalog)
        {
            bool isConsistent = catalog.Validate(out string error);
            WeatherCheckLog.Report("CatalogConsistency", isConsistent, isConsistent ? "layout and profiles match" : error);

            if (!isConsistent)
            {
                return false;
            }

            bool isAllPassed = CheckCoverage(catalog);
            isAllPassed &= CheckSnapshots(catalog);
            return isAllPassed;
        }

        private static bool CheckCoverage(WeatherCatalog catalog)
        {
            bool isPassed = true;
            string missing = string.Empty;

            foreach (WeatherType type in Enum.GetValues(typeof(WeatherType)))
            {
                if (!catalog.TryGetProfile(type, out _))
                {
                    isPassed = false;
                    missing += $"{type} ";
                }
            }

            WeatherCheckLog.Report("CatalogCoverage", isPassed, isPassed ? "every WeatherType has a profile" : $"missing: {missing}");
            return isPassed;
        }

        private static bool CheckSnapshots(WeatherCatalog catalog)
        {
            var snapshots = new List<WeatherSnapshot>();
            bool isPassed = true;

            foreach (WeatherType type in Enum.GetValues(typeof(WeatherType)))
            {
                if (!catalog.TryGetProfile(type, out WeatherProfile profile)
                    || !WeatherSnapshot.TryCreate(catalog.Layout, profile, out WeatherSnapshot snapshot))
                {
                    isPassed = false;
                    continue;
                }

                bool isValid = WaveModel.IsValid(snapshot.WaveSpan);
                WeatherCheckLog.Report($"Snapshot_{type}", isValid, $"steepness sum={profile.GetTotalSteepness():0.00}");
                isPassed &= isValid;
                snapshots.Add(snapshot);
            }

            isPassed &= CheckPairMidpoints(snapshots, catalog.Layout.Count);
            return isPassed;
        }

        private static bool CheckPairMidpoints(List<WeatherSnapshot> snapshots, int slotCount)
        {
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(slotCount);
            bool isPassed = true;

            for (int i = 0; i < snapshots.Count; i++)
            {
                for (int j = 0; j < snapshots.Count; j++)
                {
                    WeatherSnapshot a = snapshots[i];
                    WeatherSnapshot b = snapshots[j];

                    isPassed &= WeatherSnapshot.TryLerp(in a, in b, MidT, ref result) && WaveModel.IsValid(result.WaveSpan);
                }
            }

            WeatherCheckLog.Report("PairMidpoints", isPassed, $"{snapshots.Count * snapshots.Count} pairs pass WaveModel.IsValid");
            return isPassed;
        }
    }
}
