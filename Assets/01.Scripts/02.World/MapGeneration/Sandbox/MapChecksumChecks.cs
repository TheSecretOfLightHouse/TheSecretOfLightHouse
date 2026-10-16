using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.MapGeneration.Server;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class MapChecksumChecks
    {
        private const int BaseSeed = 1000;
        private const int SeedCount = 20;
        private const uint FirstActor = 1;
        private const uint SecondActor = 2;

        public static bool RunAll(MapGenerationConfig config)
        {
            MapBuilder builder = MapBuilder.CreateDefault(config);
            uint contentHash = MapContentHasher.Compute(config);

            bool isAllPassed = CheckClientLoadMatches(builder, config, contentHash);
            isAllPassed &= CheckMismatchKinds(builder, contentHash);
            isAllPassed &= CheckSeedsChangeHash(builder, contentHash);
            isAllPassed &= CheckTracker(builder, contentHash);
            return isAllPassed;
        }

        private static bool CheckClientLoadMatches(MapBuilder builder, MapGenerationConfig config, uint contentHash)
        {
            int failed = 0;
            for (int i = 0; i < SeedCount; i++)
            {
                MapSeedInfo info = MapSeedInfo.From(builder.Build(BaseSeed + i, true), contentHash);
                MapChecksumResult result = MapLayoutLoader.Load(builder, config, info, out _, out _);
                failed += result == MapChecksumResult.Match ? 0 : 1;
            }

            MapCheckLog.Report("Client load matches server", failed == 0, $"failed={failed} of {SeedCount}");
            return failed == 0;
        }

        private static bool CheckMismatchKinds(MapBuilder builder, uint contentHash)
        {
            MapSeedInfo info = MapSeedInfo.From(builder.Build(BaseSeed, true), contentHash);
            bool isMatch = info.Verify(info.LayoutHash, contentHash) == MapChecksumResult.Match;
            bool isLayout = info.Verify(info.LayoutHash + 1, contentHash) == MapChecksumResult.LayoutMismatch;
            bool isContent = info.Verify(info.LayoutHash, contentHash + 1) == MapChecksumResult.ContentMismatch;
            bool isPassed = isMatch && isLayout && isContent;
            MapCheckLog.Report("Verify result kinds", isPassed, $"match={isMatch}, layout={isLayout}, content={isContent}");
            return isPassed;
        }

        private static bool CheckSeedsChangeHash(MapBuilder builder, uint contentHash)
        {
            HashSet<uint> hashes = new HashSet<uint>();
            for (int i = 0; i < SeedCount; i++)
            {
                hashes.Add(MapSeedInfo.From(builder.Build(BaseSeed + i, true), contentHash).LayoutHash);
            }

            bool isPassed = hashes.Count >= SeedCount - 1;
            MapCheckLog.Report("Different seeds different hash", isPassed, $"distinct={hashes.Count} of {SeedCount}");
            return isPassed;
        }

        private static bool CheckTracker(MapBuilder builder, uint contentHash)
        {
            MapSeedInfo info = MapSeedInfo.From(builder.Build(BaseSeed, true), contentHash);
            uint[] roster = { FirstActor, SecondActor };
            WorldReadyTracker tracker = new WorldReadyTracker();
            int changed = 0;
            tracker.Changed += () => changed++;

            tracker.Submit(FirstActor, info, info.LayoutHash, contentHash);
            bool isPartial = !tracker.AreAllReady(roster);
            tracker.Submit(SecondActor, info, info.LayoutHash + 1, contentHash);
            bool isMismatchBlocked = !tracker.AreAllReady(roster);
            tracker.Submit(SecondActor, info, info.LayoutHash, contentHash);
            tracker.Submit(SecondActor, info, info.LayoutHash, contentHash);
            bool isAll = tracker.AreAllReady(roster);
            tracker.Clear();
            bool isCleared = !tracker.AreAllReady(roster);

            bool isPassed = isPartial && isMismatchBlocked && isAll && isCleared && changed == 2;
            MapCheckLog.Report("Ready tracker", isPassed, $"partial={isPartial}, blocked={isMismatchBlocked}, all={isAll}, cleared={isCleared}, changed={changed}");
            return isPassed;
        }
    }
}
