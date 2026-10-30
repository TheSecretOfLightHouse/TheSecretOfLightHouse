using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Debris.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class ShoreSpotSearchChecks
    {
        private const float MinHeight = 1f;
        private const float MaxSlope = 35f;
        private const float SearchRadius = 3f;
        private const float SearchStep = 0.5f;
        private const float FlatHeight = 2f;
        private const float RockHeight = 5f;
        private const float Tolerance = 0.001f;
        private const float Separation = 0.6f;

        public static bool RunAll()
        {
            var preferred = new Vector2(8.5f, 0f);
            var inward = new Vector2(-1f, 0f);
            var flat = new RockGround(float.MaxValue, float.MaxValue);
            var rock = new RockGround(8.3f, 8.7f);
            var stripes = new StripeGround();

            bool isKept = ShoreSpotSearch.TryFind(flat, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, SearchStep, default, 0f, out Vector2 keptPoint, out float keptHeight);
            bool isMoved = ShoreSpotSearch.TryFind(rock, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, SearchStep, default, 0f, out Vector2 movedPoint, out float movedHeight);
            bool isDisabled = ShoreSpotSearch.TryFind(rock, 0, preferred, inward, MinHeight, MaxSlope, 0f, SearchStep, default, 0f, out _, out _);
            bool isBlocked = ShoreSpotSearch.TryFind(stripes, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, SearchStep, default, 0f, out _, out _);
            bool isNullSafe = !ShoreSpotSearch.TryFind(null, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, SearchStep, default, 0f, out _, out _);
            bool isStepSafe = !ShoreSpotSearch.TryFind(rock, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, 0f, default, 0f, out _, out _);
            var taken = new[] { preferred };
            bool isSeparated = ShoreSpotSearch.TryFind(flat, 0, preferred, inward, MinHeight, MaxSlope, SearchRadius, SearchStep, taken, Separation, out Vector2 apartPoint, out _)
                && (apartPoint - preferred).magnitude >= Separation;

            bool isPassed =
                isKept && (keptPoint - preferred).sqrMagnitude <= Tolerance * Tolerance && Mathf.Abs(keptHeight - FlatHeight) <= Tolerance
                && isMoved && (movedPoint - preferred).magnitude <= SearchRadius && Mathf.Abs(movedHeight - FlatHeight) <= Tolerance && movedPoint.x < 8.3f
                && !isDisabled
                && !isBlocked
                && isNullSafe
                && isStepSafe
                && isSeparated;

            Debug.Log($"[{nameof(ShoreSpotSearchChecks)}] Search {(isPassed ? "PASS" : "FAIL")} kept/moved/disabled/blocked/null/step");
            return isPassed;
        }

        private sealed class RockGround : IGroundHeightSource
        {
            private const float DiscRadius = 10f;

            private readonly float _rockMinX;
            private readonly float _rockMaxX;

            public RockGround(float rockMinX, float rockMaxX)
            {
                _rockMinX = rockMinX;
                _rockMaxX = rockMaxX;
            }

            public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
            {
                bool isInside = x * x + z * z <= DiscRadius * DiscRadius;
                bool isRock = x >= _rockMinX && x <= _rockMaxX;
                height = isInside ? (isRock ? RockHeight : FlatHeight) : 0f;
                return isInside;
            }
        }

        private sealed class StripeGround : IGroundHeightSource
        {
            private const float DiscRadius = 10f;
            private const float StripeWidth = 0.5f;

            public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
            {
                bool isInside = x * x + z * z <= DiscRadius * DiscRadius;
                bool isHigh = Mathf.FloorToInt(x / StripeWidth) % 2 == 0;
                height = isInside ? (isHigh ? RockHeight : FlatHeight) : 0f;
                return isInside;
            }
        }
    }
}
