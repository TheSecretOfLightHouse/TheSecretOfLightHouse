using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class MapBuilderChecks
    {
        private const int SeedCount = 200;
        private const int BaseSeed = 1000;
        private const float MinDistinctRatio = 0.9f;

        public static bool RunAll(MapGenerationConfig config)
        {
            MapBuilder builder = MapBuilder.CreateDefault(config);
            List<MapBuildResult> results = BuildMany(builder);

            bool isAllPassed = CheckAllValid(results);
            isAllPassed &= CheckDeterminism(builder);
            isAllPassed &= CheckSeedsDiffer(results);
            isAllPassed &= CheckClientMatchesServer(builder, results);
            isAllPassed &= MapLayoutRuleChecks.CheckQuotas(config, results);
            isAllPassed &= MapLayoutRuleChecks.CheckSameTypeSpacing(config, results);
            isAllPassed &= MapLayoutRuleChecks.CheckGeometry(config, results);
            return isAllPassed;
        }

        private static List<MapBuildResult> BuildMany(MapBuilder builder)
        {
            List<MapBuildResult> results = new List<MapBuildResult>();
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < SeedCount; i++)
            {
                results.Add(builder.Build(BaseSeed + i, true));
            }

            stopwatch.Stop();
            MapCheckLog.Report("Build timing", true, $"{SeedCount} builds, avg {stopwatch.Elapsed.TotalMilliseconds / SeedCount:F2} ms");
            return results;
        }

        private static bool CheckAllValid(List<MapBuildResult> results)
        {
            int invalid = 0;
            int retried = 0;
            int maxAttempts = 0;
            for (int i = 0; i < results.Count; i++)
            {
                invalid += results[i].IsValid ? 0 : 1;
                retried += results[i].Attempts > 1 ? 1 : 0;
                maxAttempts = System.Math.Max(maxAttempts, results[i].Attempts);
            }

            bool isPassed = invalid == 0;
            MapCheckLog.Report("All seeds valid", isPassed, $"invalid={invalid}, retried={retried}, maxAttempts={maxAttempts} of {results.Count}");
            return isPassed;
        }

        private static bool CheckDeterminism(MapBuilder builder)
        {
            MapBuildResult a = builder.Build(BaseSeed, true);
            MapBuildResult b = builder.Build(BaseSeed, true);
            bool isPassed = a.Layout != null && b.Layout != null && a.Layout.Hash == b.Layout.Hash && a.Seed == b.Seed && a.Attempts == b.Attempts;
            MapCheckLog.Report("Same seed same layout", isPassed, $"{(a.Layout != null ? a.Layout.Hash : 0)} vs {(b.Layout != null ? b.Layout.Hash : 0)}");
            return isPassed;
        }

        private static bool CheckSeedsDiffer(List<MapBuildResult> results)
        {
            HashSet<uint> hashes = new HashSet<uint>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Layout != null)
                {
                    hashes.Add(results[i].Layout.Hash);
                }
            }

            bool isPassed = hashes.Count >= results.Count * MinDistinctRatio;
            MapCheckLog.Report("Different seeds differ", isPassed, $"distinct={hashes.Count} of {results.Count}");
            return isPassed;
        }

        private static bool CheckClientMatchesServer(MapBuilder builder, List<MapBuildResult> results)
        {
            int mismatches = 0;
            for (int i = 0; i < results.Count; i++)
            {
                MapBuildResult client = builder.Build(results[i].Seed, false);
                if (client.Layout == null || results[i].Layout == null || client.Layout.Hash != results[i].Layout.Hash)
                {
                    mismatches++;
                }
            }

            bool isPassed = mismatches == 0;
            MapCheckLog.Report("Client (no validation) matches server", isPassed, $"mismatches={mismatches} of {results.Count}");
            return isPassed;
        }
    }
}
