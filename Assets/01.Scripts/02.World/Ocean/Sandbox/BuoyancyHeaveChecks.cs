using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class BuoyancyHeaveChecks
    {
        private const int PointCount = 4;
        private const float Mass = 10f;
        private const float BodyHeight = 1f;
        private const float Thickness = 0.5f;
        private const float RiseHeight = 1f;
        private const float DepthTolerance = 0.02f;
        private const string HalfFollowJson = "{\"_heaveFollow\":0.5,\"_heaveBaselineSeconds\":5}";
        private const string FullFollowJson = "{\"_heaveFollow\":1,\"_heaveBaselineSeconds\":5}";

        public static bool RunAll()
        {
            var host = new GameObject("BuoyancyHeaveChecks");
            host.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var body = host.AddComponent<Rigidbody>();
                body.mass = Mass;

                bool isPassed = true;

                isPassed &= CheckAttenuated(body);
                isPassed &= CheckFullFollow(body);
                isPassed &= CheckReset(body);

                return isPassed;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static bool CheckAttenuated(Rigidbody body)
        {
            var applier = new BuoyancyForceApplier(PointCount);
            var profile = JsonUtility.FromJson<BuoyancyProfile>(HalfFollowJson);

            bool isEvaluated = TryDepth(applier, body, profile, 0f, out _);
            isEvaluated &= TryDepth(applier, body, profile, RiseHeight, out float depth);

            float expected = RiseHeight * profile.HeaveFollow;
            bool isPassed = isEvaluated && Mathf.Abs(depth - expected) <= DepthTolerance;

            WaveCheckLog.Report(
                "BuoyancyHeaveAttenuated",
                isPassed,
                $"depth={depth:F3}, expected={expected:F3}");

            return isPassed;
        }

        private static bool CheckFullFollow(Rigidbody body)
        {
            var applier = new BuoyancyForceApplier(PointCount);
            var profile = JsonUtility.FromJson<BuoyancyProfile>(FullFollowJson);

            bool isEvaluated = TryDepth(applier, body, profile, 0f, out _);
            isEvaluated &= TryDepth(applier, body, profile, RiseHeight, out float depth);

            bool isPassed = isEvaluated && Mathf.Abs(depth - RiseHeight) <= DepthTolerance;

            WaveCheckLog.Report(
                "BuoyancyHeaveFull",
                isPassed,
                $"depth={depth:F3}, expected={RiseHeight:F3}");

            return isPassed;
        }

        private static bool CheckReset(Rigidbody body)
        {
            var applier = new BuoyancyForceApplier(PointCount);
            var profile = JsonUtility.FromJson<BuoyancyProfile>(HalfFollowJson);

            bool isEvaluated = TryDepth(applier, body, profile, 0f, out _);

            applier.Reset();

            isEvaluated &= TryDepth(applier, body, profile, RiseHeight, out float depth);

            bool isPassed = isEvaluated && Mathf.Abs(depth - RiseHeight) <= DepthTolerance;

            WaveCheckLog.Report(
                "BuoyancyHeaveReset",
                isPassed,
                $"depth={depth:F3}, expected={RiseHeight:F3}");

            return isPassed;
        }

        private static bool TryDepth(
            BuoyancyForceApplier applier,
            Rigidbody body,
            BuoyancyProfile profile,
            float waterHeight,
            out float depth)
        {
            var points = new Vector3[PointCount];
            var heights = new float[PointCount];

            for (int i = 0; i < PointCount; i++)
            {
                points[i] = new Vector3(i, 0f, 0f);
                heights[i] = waterHeight;
            }

            bool isApplied = applier.TryApply(
                body,
                profile,
                BodyHeight,
                Thickness,
                BuoyancySolver.NoTargetDepth,
                points,
                heights,
                out BuoyancyReading reading);

            depth = reading.MeanDepth;

            return isApplied;
        }
    }
}
