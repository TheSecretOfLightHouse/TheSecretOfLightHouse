using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WindFieldChecks
    {
        private const float Coefficient = 0.5f;
        private const float Tolerance = 1e-3f;

        public static bool RunAll()
        {
            bool isAllPassed = CheckDirectionAndScale();

            isAllPassed &= CheckRelativeVelocity();
            isAllPassed &= CheckNoSail();
            isAllPassed &= CheckMissingSource();

            return isAllPassed;
        }

        private static bool CheckDirectionAndScale()
        {
            var field = new WindField(new FixedWind(new Vector2(10f, 0f)), Coefficient);

            Vector3 force = field.ForceAt(Vector3.zero, Vector3.zero, 2f, 0d);
            Vector3 doubled = field.ForceAt(Vector3.zero, Vector3.zero, 4f, 0d);
            float expected = Coefficient * 2f * 10f * 10f;

            bool isPassed = IsNear(force, new Vector3(expected, 0f, 0f)) && IsNear(doubled, new Vector3(expected * 2f, 0f, 0f));
            WaveCheckLog.Report("WindDirectionScale", isPassed, $"force={force}, doubled={doubled}, expected={expected}");
            return isPassed;
        }

        private static bool CheckRelativeVelocity()
        {
            var field = new WindField(new FixedWind(new Vector2(10f, 0f)), Coefficient);

            Vector3 sameSpeed = field.ForceAt(Vector3.zero, new Vector3(10f, 0f, 0f), 1f, 0d);
            Vector3 headWind = field.ForceAt(Vector3.zero, new Vector3(-10f, 0f, 0f), 1f, 0d);
            Vector3 verticalOnly = field.ForceAt(Vector3.zero, new Vector3(0f, 5f, 0f), 1f, 0d);
            Vector3 still = field.ForceAt(Vector3.zero, Vector3.zero, 1f, 0d);

            bool isPassed = IsNear(sameSpeed, Vector3.zero)
                && IsNear(headWind, new Vector3(Coefficient * 20f * 20f, 0f, 0f))
                && IsNear(verticalOnly, still);
            WaveCheckLog.Report("WindRelativeVelocity", isPassed, $"same={sameSpeed}, head={headWind}, vertical={verticalOnly}, still={still}");
            return isPassed;
        }

        private static bool CheckNoSail()
        {
            var field = new WindField(new FixedWind(new Vector2(10f, 5f)), Coefficient);

            Vector3 force = field.ForceAt(Vector3.zero, Vector3.zero, 0f, 0d);

            bool isPassed = IsNear(force, Vector3.zero);
            WaveCheckLog.Report("WindNoSail", isPassed, $"force={force}");
            return isPassed;
        }

        private static bool CheckMissingSource()
        {
            var field = new WindField(null, Coefficient);

            Vector3 force = field.ForceAt(Vector3.zero, Vector3.zero, 1f, 0d);

            bool isPassed = !field.IsInfluencing(Vector3.zero) && IsNear(force, Vector3.zero);
            WaveCheckLog.Report("WindMissingSource", isPassed, $"influencing={field.IsInfluencing(Vector3.zero)}, force={force}");
            return isPassed;
        }

        private static bool IsNear(Vector3 actual, Vector3 expected)
        {
            return (actual - expected).magnitude <= Tolerance;
        }

        private sealed class FixedWind : IWindSource
        {
            private readonly Vector2 _wind;

            public FixedWind(Vector2 wind)
            {
                _wind = wind;
            }

            public Vector2 GetWind(double time)
            {
                return _wind;
            }
        }
    }
}
