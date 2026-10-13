using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class SeededRandomChecks
    {
        private const int Seed = 12345;
        private const int SequenceLength = 1000;
        private const int ShortSequenceLength = 64;
        private const int FloatSampleCount = 100000;
        private const float FloatMeanTolerance = 0.01f;
        private const int IntMin = -3;
        private const int IntMaxExclusive = 4;
        private const int IntSampleCount = 70000;
        private const float BucketTolerance = 0.1f;
        private const int ShuffleLength = 100;
        private const int ForkParentSkip = 100;
        private const int ForkStreamId = 7;

        private static readonly uint[] GoldenSequence = { 1206779164u, 886047776u, 951604652u, 4004504286u };
        private static readonly uint[] GoldenForkSequence = { 1568776773u, 1197176695u };
        private static readonly int[] EdgeSeeds = { 0, -1, int.MinValue, int.MaxValue };

        public static bool RunAll()
        {
            bool isAllPassed = CheckSameSeed();

            isAllPassed &= CheckDifferentSeed();
            isAllPassed &= CheckEdgeSeeds();
            isAllPassed &= CheckFloatRange();
            isAllPassed &= CheckIntRange();
            isAllPassed &= CheckFork();
            isAllPassed &= CheckShuffle();
            isAllPassed &= CheckGolden();

            return isAllPassed;
        }

        private static bool CheckSameSeed()
        {
            SeededRandom a = new SeededRandom(Seed);
            SeededRandom b = new SeededRandom(Seed);
            int mismatches = 0;

            for (int i = 0; i < SequenceLength; i++)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    mismatches++;
                }
            }

            MapCheckLog.Report("SameSeed", mismatches == 0, $"draws={SequenceLength}, mismatches={mismatches}");
            return mismatches == 0;
        }

        private static bool CheckDifferentSeed()
        {
            SeededRandom a = new SeededRandom(Seed);
            SeededRandom b = new SeededRandom(Seed + 1);
            int equalCount = 0;

            for (int i = 0; i < ShortSequenceLength; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    equalCount++;
                }
            }

            bool isPassed = equalCount <= 1;
            MapCheckLog.Report("DifferentSeed", isPassed, $"draws={ShortSequenceLength}, equal={equalCount}");
            return isPassed;
        }

        private static bool CheckEdgeSeeds()
        {
            int stuck = 0;

            for (int i = 0; i < EdgeSeeds.Length; i++)
            {
                SeededRandom random = new SeededRandom(EdgeSeeds[i]);
                if (random.NextUInt() == random.NextUInt())
                {
                    stuck++;
                }
            }

            MapCheckLog.Report("EdgeSeeds", stuck == 0, $"seeds={EdgeSeeds.Length}, stuck={stuck}");
            return stuck == 0;
        }

        private static bool CheckFloatRange()
        {
            SeededRandom random = new SeededRandom(Seed);
            float min = float.MaxValue;
            float max = float.MinValue;
            double sum = 0d;

            for (int i = 0; i < FloatSampleCount; i++)
            {
                float value = random.NextFloat();
                min = value < min ? value : min;
                max = value > max ? value : max;
                sum += value;
            }

            double mean = sum / FloatSampleCount;
            bool isPassed = min >= 0f && max < 1f && mean > 0.5d - FloatMeanTolerance && mean < 0.5d + FloatMeanTolerance;
            MapCheckLog.Report("FloatRange", isPassed, $"min={min:F6}, max={max:F6}, mean={mean:F4}");
            return isPassed;
        }

        private static bool CheckIntRange()
        {
            SeededRandom random = new SeededRandom(Seed);
            int bucketCount = IntMaxExclusive - IntMin;
            int[] buckets = new int[bucketCount];
            int outOfRange = 0;

            for (int i = 0; i < IntSampleCount; i++)
            {
                int value = random.NextInt(IntMin, IntMaxExclusive);
                if (value < IntMin || value >= IntMaxExclusive)
                {
                    outOfRange++;
                    continue;
                }

                buckets[value - IntMin]++;
            }

            float expected = (float)IntSampleCount / bucketCount;
            int skewed = 0;
            for (int i = 0; i < bucketCount; i++)
            {
                if (buckets[i] < expected * (1f - BucketTolerance) || buckets[i] > expected * (1f + BucketTolerance))
                {
                    skewed++;
                }
            }

            int singleMisses = 0;
            for (int i = 0; i < ShortSequenceLength; i++)
            {
                if (random.NextInt(5, 6) != 5)
                {
                    singleMisses++;
                }
            }

            bool isPassed = outOfRange == 0 && skewed == 0 && singleMisses == 0;
            MapCheckLog.Report("IntRange", isPassed, $"outOfRange={outOfRange}, skewedBuckets={skewed}, singleMisses={singleMisses}");
            return isPassed;
        }

        private static bool CheckFork()
        {
            SeededRandom parent = new SeededRandom(Seed);
            SeededRandom before = parent.Fork(ForkStreamId);
            for (int i = 0; i < ForkParentSkip; i++)
            {
                parent.NextUInt();
            }

            SeededRandom after = parent.Fork(ForkStreamId);
            int independenceMismatches = 0;
            for (int i = 0; i < ShortSequenceLength; i++)
            {
                if (before.NextUInt() != after.NextUInt())
                {
                    independenceMismatches++;
                }
            }

            SeededRandom streamA = parent.Fork(1);
            SeededRandom streamB = parent.Fork(2);
            int sameAcrossStreams = 0;
            for (int i = 0; i < ShortSequenceLength; i++)
            {
                if (streamA.NextUInt() == streamB.NextUInt())
                {
                    sameAcrossStreams++;
                }
            }

            bool isPassed = independenceMismatches == 0 && sameAcrossStreams <= 1;
            MapCheckLog.Report("Fork", isPassed, $"parentIndependenceMismatches={independenceMismatches}, sameAcrossStreams={sameAcrossStreams}");
            return isPassed;
        }

        private static bool CheckShuffle()
        {
            List<int> first = CreateSequence();
            List<int> second = CreateSequence();
            new SeededRandom(Seed).Shuffle(first);
            new SeededRandom(Seed).Shuffle(second);

            int sameOrderMismatches = 0;
            int movedCount = 0;
            bool[] seen = new bool[ShuffleLength];
            bool isPermutation = true;

            for (int i = 0; i < ShuffleLength; i++)
            {
                sameOrderMismatches += first[i] != second[i] ? 1 : 0;
                movedCount += first[i] != i ? 1 : 0;

                if (first[i] < 0 || first[i] >= ShuffleLength || seen[first[i]])
                {
                    isPermutation = false;
                    continue;
                }

                seen[first[i]] = true;
            }

            bool isPassed = sameOrderMismatches == 0 && isPermutation && movedCount > 0;
            MapCheckLog.Report("Shuffle", isPassed, $"mismatches={sameOrderMismatches}, isPermutation={isPermutation}, moved={movedCount}");
            return isPassed;
        }

        private static bool CheckGolden()
        {
            SeededRandom random = new SeededRandom(Seed);
            int mismatches = 0;

            for (int i = 0; i < GoldenSequence.Length; i++)
            {
                mismatches += random.NextUInt() != GoldenSequence[i] ? 1 : 0;
            }

            SeededRandom fork = new SeededRandom(Seed).Fork(ForkStreamId);
            for (int i = 0; i < GoldenForkSequence.Length; i++)
            {
                mismatches += fork.NextUInt() != GoldenForkSequence[i] ? 1 : 0;
            }

            MapCheckLog.Report("Golden", mismatches == 0, $"seed={Seed}, mismatches={mismatches}");
            return mismatches == 0;
        }

        private static List<int> CreateSequence()
        {
            List<int> list = new List<int>(ShuffleLength);
            for (int i = 0; i < ShuffleLength; i++)
            {
                list.Add(i);
            }

            return list;
        }
    }
}
