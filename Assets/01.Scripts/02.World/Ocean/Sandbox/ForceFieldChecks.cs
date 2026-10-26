using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class ForceFieldChecks
    {
        private static readonly Vector3 Inside = new Vector3(5f, 0f, 5f);
        private static readonly Vector3 Outside = new Vector3(50f, 0f, 50f);

        public static bool RunAll()
        {
            bool isAllPassed = CheckBoundsFilter();

            isAllPassed &= CheckInfluenceFilter();
            isAllPassed &= CheckDynamicSum();
            isAllPassed &= CheckRegistration();
            isAllPassed &= CheckLegacyOverload();
            isAllPassed &= CheckRouteFilter();

            return isAllPassed;
        }

        private static bool CheckBoundsFilter()
        {
            var registry = new ForceFieldRegistry();
            registry.Register(new TestField(new Rect(0f, 0f, 10f, 10f), true, Vector3.right), false);

            Vector3 inside = registry.AccumulateForce(Inside, Vector3.zero, 1f, 0d);
            Vector3 outside = registry.AccumulateForce(Outside, Vector3.zero, 1f, 0d);

            bool isPassed = inside == Vector3.right && outside == Vector3.zero;
            WaveCheckLog.Report("BoundsFilter", isPassed, $"inside={inside}, outside={outside}");
            return isPassed;
        }

        private static bool CheckInfluenceFilter()
        {
            var registry = new ForceFieldRegistry();
            registry.Register(new TestField(default, false, Vector3.forward, false), true);

            Vector3 force = registry.AccumulateForce(Inside, Vector3.zero, 1f, 0d);

            bool isPassed = force == Vector3.zero;
            WaveCheckLog.Report("InfluenceFilter", isPassed, $"force={force}");
            return isPassed;
        }

        private static bool CheckDynamicSum()
        {
            var registry = new ForceFieldRegistry();
            registry.Register(new TestField(new Rect(0f, 0f, 10f, 10f), true, Vector3.right), false);
            registry.Register(new TestField(default, false, Vector3.forward), true);

            Vector3 force = registry.AccumulateForce(Inside, Vector3.zero, 1f, 0d);
            Vector3 farForce = registry.AccumulateForce(Outside, Vector3.zero, 1f, 0d);

            bool isPassed = force == Vector3.right + Vector3.forward && farForce == Vector3.forward;
            WaveCheckLog.Report("DynamicSum", isPassed, $"inside={force}, outside={farForce}");
            return isPassed;
        }

        private static bool CheckRegistration()
        {
            var registry = new ForceFieldRegistry();
            var field = new TestField(default, false, Vector3.up);

            bool isFirstAdded = registry.Register(field, false);
            bool isDuplicateAdded = registry.Register(field, true);
            bool isNullAdded = registry.Register(null, false);
            bool isRemoved = registry.Unregister(field);
            bool isRemovedAgain = registry.Unregister(field);

            bool isPassed = isFirstAdded && !isDuplicateAdded && !isNullAdded && isRemoved && !isRemovedAgain
                && registry.StaticCount == 0 && registry.DynamicCount == 0;
            WaveCheckLog.Report("Registration", isPassed, $"add={isFirstAdded}, dup={isDuplicateAdded}, null={isNullAdded}, remove={isRemoved}/{isRemovedAgain}");
            return isPassed;
        }

        private static bool CheckLegacyOverload()
        {
            var registry = new ForceFieldRegistry();
            registry.Register(new TestField(default, false, Vector3.up), true);

            Vector3 legacy = registry.AccumulateForce(Inside, 0d);
            Vector3 explicitCall = registry.AccumulateForce(Inside, Vector3.zero, 1f, 0d);

            bool isPassed = legacy == explicitCall;
            WaveCheckLog.Report("LegacyOverload", isPassed, $"legacy={legacy}, explicit={explicitCall}");
            return isPassed;
        }

        private static bool CheckRouteFilter()
        {
            var registry = new ForceFieldRegistry();
            registry.Register(new TestField(default, false, Vector3.right, true, ForceFieldRoute.Impulse), true);
            registry.Register(new TestField(new Rect(0f, 0f, 10f, 10f), true, Vector3.forward, true, ForceFieldRoute.Drift), false);

            Vector3 impulse = registry.AccumulateForce(ForceFieldRoute.Impulse, Inside, Vector3.zero, 1f, 0d);
            Vector3 drift = registry.AccumulateForce(ForceFieldRoute.Drift, Inside, Vector3.zero, 1f, 0d);
            Vector3 driftOutside = registry.AccumulateForce(ForceFieldRoute.Drift, Outside, Vector3.zero, 1f, 0d);
            Vector3 all = registry.AccumulateForce(Inside, Vector3.zero, 1f, 0d);

            bool isPassed = impulse == Vector3.right && drift == Vector3.forward
                && driftOutside == Vector3.zero && all == Vector3.right + Vector3.forward;
            WaveCheckLog.Report("RouteFilter", isPassed, $"impulse={impulse}, drift={drift}, driftOutside={driftOutside}, all={all}");
            return isPassed;
        }

        private sealed class TestField : IForceField
        {
            private readonly Rect _bounds;
            private readonly bool _hasBounds;
            private readonly Vector3 _force;
            private readonly bool _isInfluencing;

            public TestField(Rect bounds, bool hasBounds, Vector3 force, bool isInfluencing = true, ForceFieldRoute route = ForceFieldRoute.Impulse)
            {
                Route = route;
                _bounds = bounds;
                _hasBounds = hasBounds;
                _force = force;
                _isInfluencing = isInfluencing;
            }

            public ForceFieldRoute Route { get; }

            public bool TryGetBounds(out Rect bounds)
            {
                bounds = _bounds;
                return _hasBounds;
            }

            public bool IsInfluencing(Vector3 worldPos)
            {
                return _isInfluencing;
            }

            public Vector3 ForceAt(Vector3 worldPos, Vector3 velocity, float hullExposure, double time)
            {
                return _force;
            }
        }
    }
}
