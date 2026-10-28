using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class BreakerEventChecks
    {
        private const int SourceCount = 8;
        private const float Duration = 60f;
        private const float Step = 0.05f;

        public static bool RunAll()
        {
            bool isAllPassed = CheckTuning();
            isAllPassed &= CheckTimeline();
            isAllPassed &= CheckSwell();
            isAllPassed &= CheckSchedule();
            isAllPassed &= CheckShape();
            isAllPassed &= CheckSourceFilter();
            isAllPassed &= CheckApproach();
            return isAllPassed;
        }

        private static bool CheckApproach()
        {
            int count = CoastWaveRegistry.Segments;
            var bounds = new Vector4[1];
            var edges = new Vector4[count];
            const float radius = 50f;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float b = (i + 1) * Mathf.PI * 2f / count;
                edges[i] = new Vector4(radius * Mathf.Cos(a), radius * Mathf.Sin(a), radius * Mathf.Cos(b), radius * Mathf.Sin(b));
            }

            bounds[0] = new Vector4(0f, 0f, radius + 20f, 7f);
            Vector2 swell = new Vector2(1f, 0f);
            Vector2 west = BreakerApproach.Resolve(bounds, edges, new Vector2(-50f, 0f), swell, out float westExposure);
            Vector2 east = BreakerApproach.Resolve(bounds, edges, new Vector2(50f, 0f), swell, out float eastExposure);
            Vector2 open = BreakerApproach.Resolve(new Vector4[1], edges, new Vector2(500f, 0f), swell, out float openExposure);
            bool isPassed = west.x > 0.9f && east.x < -0.9f && westExposure > eastExposure
                && openExposure > 0.99f && open.x > 0.9f;
            WaveCheckLog.Report("Breaker.Approach", isPassed,
                $"west={west}, east={east}, exposure west={westExposure:F2} east={eastExposure:F2}, open={open}/{openExposure:F2}");
            return isPassed;
        }

        private static bool CheckSourceFilter()
        {
            BreakerWaveTuning tuning = BreakerWaveTuning.Default;
            Vector2 swell = new Vector2(1f, 0f);
            bool rock = BreakerSourceFilter.TryCreate(in tuning, 1, new Vector3(5f, 0.5f, 3f), new Vector3(1f, 1f, 2f),
                0f, swell, 1f, out BreakerSource source);
            bool isGeometryRight = rock && Mathf.Abs(source.Radius - 1f) < 0.0001f
                && Mathf.Abs(source.Width - 4f) < 0.0001f && Mathf.Abs(source.Height - 1.5f) < 0.0001f
                && source.Center == new Vector2(5f, 3f);
            bool isSubmerged = BreakerSourceFilter.TryCreate(in tuning, 2, new Vector3(0f, -3f, 0f),
                new Vector3(1f, 1f, 1f), 0f, swell, 1f, out _);
            bool isFloating = BreakerSourceFilter.TryCreate(in tuning, 3, new Vector3(0f, 5f, 0f),
                new Vector3(1f, 1f, 1f), 0f, swell, 1f, out _);
            bool isTerrain = BreakerSourceFilter.TryCreate(in tuning, 4, new Vector3(0f, 0f, 0f),
                new Vector3(40f, 3f, 40f), 0f, swell, 1f, out _);
            bool isLow = BreakerSourceFilter.TryCreate(in tuning, 5, new Vector3(0f, 0.1f, 0f),
                new Vector3(1f, 0.15f, 1f), 0f, swell, 1f, out _);
            var kept = new[] { source };
            bool isNear = BreakerSourceFilter.HasNeighbor(in tuning, kept, new Vector2(5.5f, 3f));
            bool isFar = BreakerSourceFilter.HasNeighbor(in tuning, kept, new Vector2(8f, 3f));
            bool isPassed = isGeometryRight && !isSubmerged && !isFloating && !isTerrain && !isLow && isNear && !isFar;
            WaveCheckLog.Report("Breaker.SourceFilter", isPassed,
                $"geometry={isGeometryRight}, sub={isSubmerged}, float={isFloating}, terrain={isTerrain}, low={isLow}, near={isNear}, far={isFar}");
            return isPassed;
        }

        private static bool CheckTuning()
        {
            BreakerWaveTuning tuning = BreakerWaveTuning.Default;
            BreakerWaveTuning broken = tuning;
            broken.Duration = tuning.MinInterval;
            bool isPassed = tuning.IsValid() && !broken.IsValid();
            WaveCheckLog.Report("Breaker.Tuning", isPassed, $"default={tuning.IsValid()}, broken={broken.IsValid()}");
            return isPassed;
        }

        private static bool CheckTimeline()
        {
            BreakerWaveTuning tuning = BreakerWaveTuning.Default;
            float[] ages = { -0.1f, 0.5f, 1f, 1.3f, 2f, 2.4f };
            BreakerPhase[] expected =
            {
                BreakerPhase.None, BreakerPhase.Approach, BreakerPhase.Curl,
                BreakerPhase.Impact, BreakerPhase.Rundown, BreakerPhase.None
            };
            bool isPassed = true;
            for (int i = 0; i < ages.Length; i++)
            {
                BreakerPhase phase = BreakerTimeline.Evaluate(in tuning, ages[i], out float progress);
                isPassed &= phase == expected[i] && progress >= 0f && progress <= 1f;
            }

            WaveCheckLog.Report("Breaker.Timeline", isPassed, "phase boundaries");
            return isPassed;
        }

        private static bool CheckSwell()
        {
            WaveParams[] waves =
            {
                new WaveParams { Amplitude = 0.2f, Direction = new Vector2(0f, 1f) },
                new WaveParams { Amplitude = 0.5f, Direction = new Vector2(2f, 0f) }
            };
            bool found = BreakerSwell.TryGet(waves, out Vector2 direction, out float energy);
            bool none = BreakerSwell.TryGet(Array.Empty<WaveParams>(), out _, out _);
            bool isPassed = found && !none
                && Mathf.Abs(direction.x - 1f) < 0.0001f && Mathf.Abs(energy - 0.7f) < 0.0001f;
            WaveCheckLog.Report("Breaker.Swell", isPassed, $"dir={direction}, energy={energy:F3}, empty={none}");
            return isPassed;
        }

        private static bool CheckSchedule()
        {
            BreakerWaveTuning tuning = BreakerWaveTuning.Default;
            var sources = new BreakerSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                sources[i] = new BreakerSource
                {
                    Id = 100 + i * 7, Center = new Vector2(i * 10f, 0f), Approach = new Vector2(1f, 0f), Exposure = 1f, Radius = 2f, Width = 3f, Height = 2f
                };
            }

            Vector2 swell = new Vector2(1f, 0f);
            var events = new BreakerEvent[SourceCount];
            var wasActive = new bool[SourceCount];
            var starts = new int[SourceCount];
            var firstSeen = new float[SourceCount];
            bool hasDuplicate = false;
            bool isFacingUpwind = true;
            for (int step = 0; step * Step <= Duration; step++)
            {
                float time = step * Step;
                int count = BreakerEventSchedule.Collect(in tuning, sources, 1f, time, events);
                var isActive = new bool[SourceCount];
                for (int e = 0; e < count; e++)
                {
                    int index = (events[e].SourceId - 100) / 7;
                    hasDuplicate |= isActive[index];
                    isActive[index] = true;
                    isFacingUpwind &= Vector2.Dot(events[e].ImpactPoint - sources[index].Center, swell) < 0f;
                    if (!wasActive[index] && step > 0)
                    {
                        starts[index]++;
                        if (starts[index] == 1)
                        {
                            firstSeen[index] = time;
                        }
                    }
                }

                wasActive = isActive;
            }

            bool isCountInRange = true;
            int distinct = 0;
            for (int i = 0; i < SourceCount; i++)
            {
                isCountInRange &= starts[i] >= Mathf.FloorToInt(Duration / tuning.MaxInterval) - 1
                    && starts[i] <= Mathf.CeilToInt(Duration / tuning.MinInterval) + 1;
                bool isNew = true;
                for (int j = 0; j < i; j++)
                {
                    isNew &= Mathf.Abs(firstSeen[i] - firstSeen[j]) > 0.001f;
                }

                distinct += isNew ? 1 : 0;
            }

            int low = BreakerEventSchedule.Collect(in tuning, sources, 0.01f, 1.0, events);
            var first = new BreakerEvent[SourceCount];
            var second = new BreakerEvent[SourceCount];
            int a = BreakerEventSchedule.Collect(in tuning, sources, 1f, 12.3, first);
            int b = BreakerEventSchedule.Collect(in tuning, sources, 1f, 12.3, second);
            bool isDeterministic = a == b;
            for (int i = 0; i < a && isDeterministic; i++)
            {
                isDeterministic &= first[i].SourceId == second[i].SourceId && first[i].Age == second[i].Age
                    && first[i].Strength == second[i].Strength;
            }

            bool isPassed = !hasDuplicate && isFacingUpwind && isCountInRange && distinct >= SourceCount - 1
                && low == 0 && isDeterministic;
            WaveCheckLog.Report("Breaker.Schedule", isPassed,
                $"dup={hasDuplicate}, upwind={isFacingUpwind}, counts={string.Join(",", starts)}, distinct={distinct}, lowEnergy={low}, deterministic={isDeterministic}");
            return isPassed;
        }

        private static bool CheckShape()
        {
            BreakerShape spilling = BreakerShape.FromSteepness(0f);
            BreakerShape plunging = BreakerShape.FromSteepness(1f);
            BreakerShape clamped = BreakerShape.FromSteepness(5f);
            bool isPassed = spilling.Curl < plunging.Curl && spilling.LipLength < plunging.LipLength
                && spilling.SheetHeight < plunging.SheetHeight && spilling.FoamDensity < plunging.FoamDensity
                && clamped.Curl == plunging.Curl;
            WaveCheckLog.Report("Breaker.Shape", isPassed, $"spillCurl={spilling.Curl}, plungeCurl={plunging.Curl}");
            return isPassed;
        }
    }
}
