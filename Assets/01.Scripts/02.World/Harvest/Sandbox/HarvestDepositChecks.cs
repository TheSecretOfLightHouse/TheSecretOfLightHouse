using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Harvest.Server;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class HarvestDepositChecks
    {
        public static bool RunAll()
        {
            bool isPassed = CheckReservations();
            isPassed &= CheckRegen();
            isPassed &= CheckRegistryLifetime();
            isPassed &= CheckPolicy();
            isPassed &= HarvestNotificationChecks.RunAll();
            isPassed &= DepositDistributorChecks.RunAll();
            return isPassed;
        }

        public static void Report(
            string testName,
            bool isPassed,
            string detail)
        {
            string message =
                $"[{nameof(HarvestDepositChecks)}] {testName}: "
                + $"{(isPassed ? "PASS" : "FAIL")} ({detail})";

            if (isPassed)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        private static ResourceDeposit CreateDeposit()
        {
            SpotAssignment[] assignments =
            {
                new SpotAssignment(1, 10, 20, 0.5f),
                new SpotAssignment(2, 0, 10, 0.5f),
                default
            };

            return new ResourceDeposit(assignments);
        }

        private static bool Reserve(
            ResourceDeposit deposit,
            int index,
            int requested,
            out HarvestReservation reservation)
        {
            return deposit.TryReserve(
                index,
                HarvestSpotIds.Compose(1, (ushort)index),
                Vector3.zero,
                requested,
                out reservation);
        }

        private static bool CheckReservations()
        {
            ResourceDeposit deposit = CreateDeposit();
            ResourceDeposit other = CreateDeposit();
            int notifications = 0;
            bool isReentryRejected = false;
            HarvestReservation first = null;

            deposit.RemainingChanged += (index, remaining) =>
            {
                notifications++;
                isReentryRejected = !deposit.TryCommit(first)
                    && !deposit.TryCancel(first);
            };

            bool isOk = Reserve(deposit, 0, 4, out first);
            isOk &= first != null
                && first.Granted == 4
                && first.ResourceId == 1;
            isOk &= deposit.TryGetRemaining(0, out int remaining)
                && remaining == 10;
            isOk &= notifications == 0;
            isOk &= Reserve(deposit, 0, 100, out HarvestReservation second);
            isOk &= second != null && second.Granted == 6;
            isOk &= !Reserve(deposit, 0, 1, out _);
            isOk &= !other.TryCommit(first);
            isOk &= !other.TryCancel(first);
            isOk &= deposit.TryCommit(first);
            isOk &= notifications == 1 && isReentryRejected;
            isOk &= deposit.TryGetRemaining(0, out remaining)
                && remaining == 6;
            isOk &= deposit.TryCancel(second);
            isOk &= !deposit.TryCancel(second);
            isOk &= !deposit.TryCommit(second);
            isOk &= notifications == 1;

            isOk &= Reserve(deposit, 0, 100, out HarvestReservation last);
            isOk &= last != null && last.Granted == 6;
            isOk &= deposit.TryCommit(last);
            isOk &= !Reserve(deposit, 0, 1, out _);
            isOk &= !Reserve(deposit, 1, 1, out _);
            isOk &= !Reserve(deposit, 2, 1, out _);
            isOk &= !Reserve(deposit, 0, 0, out _);
            isOk &= !Reserve(deposit, 0, -1, out _);
            isOk &= !Reserve(deposit, -1, 1, out _);
            isOk &= !Reserve(deposit, 3, 1, out _);
            isOk &= !deposit.TryCommit(null);
            isOk &= !deposit.TryCancel(null);

            Report(
                "Reservations",
                isOk,
                "partial, exclusion, foreign token, duplicate, reentry, invalid input");
            return isOk;
        }

        private static bool CheckRegen()
        {
            ResourceDeposit deposit = CreateDeposit();
            ProportionalRegenPolicy policy = new ProportionalRegenPolicy();

            bool isOk = Reserve(
                deposit, 0, 10, out HarvestReservation held);

            isOk &= deposit.Regenerate(policy, 0) == 0;
            isOk &= deposit.Regenerate(null, 1) == 0;
            isOk &= deposit.Regenerate(policy, 1) == 2;
            isOk &= deposit.TryGetRemaining(0, out int remaining)
                && remaining == 20;

            // The existing reservation must not grow after regeneration.
            isOk &= held != null && held.Granted == 10;
            isOk &= deposit.TryCancel(held);
            isOk &= deposit.TryGetRemaining(0, out remaining)
                && remaining == 20;

            isOk &= Reserve(
                deposit, 0, 100, out HarvestReservation all);
            isOk &= all != null && all.Granted == 20;
            isOk &= deposit.TryCommit(all);
            isOk &= deposit.Regenerate(policy, 1) == 2;
            isOk &= deposit.TryGetRemaining(0, out remaining)
                && remaining == 10;
            isOk &= deposit.Regenerate(policy, 5) == 1;
            isOk &= deposit.TryGetRemaining(0, out remaining)
                && remaining == 20;
            isOk &= deposit.Regenerate(policy, 1) == 0;

            Report(
                "Regenerate",
                isOk,
                "reservation preserved, cancel without inflation, room clamp");
            return isOk;
        }

        private static bool CheckRegistryLifetime()
        {
            GameObject root = new GameObject("HarvestReservationCheck");
            HarvestSpot spot = root.AddComponent<HarvestSpot>();
            HarvestSpot[] spots = { spot };
            SpotAssignment[] assignments =
            {
                new SpotAssignment(1, 1, 1, 0f)
            };

            HarvestSpotRegistry registry = new HarvestSpotRegistry();
            HarvestSpotRegistry other = new HarvestSpotRegistry();
            int spotId = HarvestSpotIds.Compose(1, 0);
            bool isOk;

            try
            {
                isOk = registry.RegisterIsland(1, spots, assignments);
                isOk &= registry.TryReserve(
                    spotId, 1, out HarvestReservation first);
                isOk &= !registry.TryReserve(spotId, 1, out _);
                isOk &= !other.TryCommit(first);

                isOk &= registry.UnregisterIsland(1);
                isOk &= !registry.TryCommit(first);
                isOk &= !registry.TryCancel(first);

                isOk &= registry.RegisterIsland(1, spots, assignments);
                isOk &= !registry.TryCommit(first);
                isOk &= !registry.TryCancel(first);
                isOk &= registry.TryGetSpot(spotId, out SpotInfo info)
                    && info.Remaining == 1;

                isOk &= registry.TryReserve(
                    spotId, 1, out HarvestReservation second);
                isOk &= registry.TryCommit(second);
                isOk &= !registry.TryCommit(second);
                isOk &= registry.TryGetSpot(spotId, out info)
                    && info.Remaining == 0;
            }
            finally
            {
                registry.UnregisterIsland(1);

                if (Application.isPlaying)
                {
                    Object.Destroy(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            Report(
                "Registry lifetime",
                isOk,
                "last unit, release, re-register, stale and foreign tokens");
            return isOk;
        }

        private static bool CheckPolicy()
        {
            ProportionalRegenPolicy policy = new ProportionalRegenPolicy();
            bool isOk = policy.GetRegenAmount(40, 0.5f, 0, 1) == 20;
            isOk &= policy.GetRegenAmount(40, 0.5f, 35, 1) == 5;
            isOk &= policy.GetRegenAmount(40, 0.5f, 0, 3) == 40;
            isOk &= policy.GetRegenAmount(40, 0f, 0, 1) == 0;
            isOk &= policy.GetRegenAmount(0, 0.5f, 0, 1) == 0;

            Report(
                "RegenPolicy",
                isOk,
                "rate of max, room clamp, multi-day, zero rate");
            return isOk;
        }
    }
}
