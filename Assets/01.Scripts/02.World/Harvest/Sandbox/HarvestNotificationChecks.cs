using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Harvest.Server;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class HarvestNotificationChecks
    {
        public static bool RunAll()
        {
            bool isPassed = CheckDepositOrder();
            isPassed &= CheckRegenOrder();
            isPassed &= CheckRegistryLifetime(false);
            isPassed &= CheckRegistryLifetime(true);
            return isPassed;
        }

        private static bool CheckDepositOrder()
        {
            ResourceDeposit deposit = new ResourceDeposit(
                new[] { new SpotAssignment(1, 3, 3, 0f) });

            int spotId = HarvestSpotIds.Compose(1, 0);
            bool isOk = deposit.TryReserve(
                0, spotId, Vector3.zero, 1, out HarvestReservation first);

            isOk &= deposit.TryReserve(
                0, spotId, Vector3.zero, 1, out HarvestReservation second);

            List<int> observed = new List<int>();
            bool hasReentered = false;
            bool isNestedCommitted = false;

            deposit.RemainingChanged += (index, remaining) =>
            {
                if (hasReentered)
                {
                    return;
                }

                hasReentered = true;
                isNestedCommitted = deposit.TryCommit(second);
            };

            deposit.RemainingChanged += (index, remaining) =>
            {
                observed.Add(remaining);
            };

            isOk &= deposit.TryCommit(first);
            isOk &= isNestedCommitted;
            isOk &= observed.Count == 2
                && observed[0] == 2
                && observed[1] == 1;
            isOk &= deposit.TryGetRemaining(0, out int actual)
                && actual == 1;
            isOk &= !deposit.TryCommit(first)
                && !deposit.TryCommit(second)
                && !deposit.TryCancel(second);

            HarvestDepositChecks.Report(
                "Notification deposit order",
                isOk,
                "observer 2 -> 1, actual 1, duplicate rejected");
            return isOk;
        }

        private static bool CheckRegenOrder()
        {
            ResourceDeposit deposit = new ResourceDeposit(
                new[] { new SpotAssignment(1, 3, 3, 1f) });

            int spotId = HarvestSpotIds.Compose(1, 0);
            bool isOk = deposit.TryReserve(
                0, spotId, Vector3.zero, 1, out HarvestReservation first);

            List<int> observed = new List<int>();
            bool hasReentered = false;
            int regenerated = 0;

            deposit.RemainingChanged += (index, remaining) =>
            {
                if (hasReentered)
                {
                    return;
                }

                hasReentered = true;
                regenerated = deposit.Regenerate(
                    new ProportionalRegenPolicy(), 1);
            };

            deposit.RemainingChanged += (index, remaining) =>
            {
                observed.Add(remaining);
            };

            isOk &= deposit.TryCommit(first);
            isOk &= regenerated == 1;
            isOk &= observed.Count == 2
                && observed[0] == 2
                && observed[1] == 3;
            isOk &= deposit.TryGetRemaining(0, out int actual)
                && actual == 3;

            HarvestDepositChecks.Report(
                "Notification regeneration order",
                isOk,
                "observer 2 -> 3, actual 3");
            return isOk;
        }

        private static bool CheckRegistryLifetime(bool shouldReplace)
        {
            GameObject firstRoot =
                new GameObject("HarvestNotificationCheckFirst");
            GameObject secondRoot =
                new GameObject("HarvestNotificationCheckSecond");

            HarvestSpotRegistry registry = new HarvestSpotRegistry();
            bool isOk = true;

            try
            {
                HarvestSpot firstSpot =
                    firstRoot.AddComponent<HarvestSpot>();
                HarvestSpot secondSpot =
                    secondRoot.AddComponent<HarvestSpot>();

                SpotAssignment[] assignments =
                {
                    new SpotAssignment(1, 3, 3, 0f)
                };

                HarvestSpot[] secondSpots = { secondSpot };
                int firstId = HarvestSpotIds.Compose(1, 0);
                int secondId = HarvestSpotIds.Compose(2, 0);

                isOk &= registry.RegisterIsland(
                    1, new[] { firstSpot }, assignments);
                isOk &= registry.RegisterIsland(
                    2, secondSpots, assignments);
                isOk &= registry.TryReserve(
                    firstId, 1, out HarvestReservation first);
                isOk &= registry.TryReserve(
                    secondId, 1, out HarvestReservation second);

                List<short> observedIslands = new List<short>();
                List<int> observedRemaining = new List<int>();
                bool hasReentered = false;
                bool isNestedOk = true;

                registry.RemainingChanged += (island, index, remaining) =>
                {
                    if (hasReentered || island != 1)
                    {
                        return;
                    }

                    hasReentered = true;
                    isNestedOk &= registry.TryCommit(second);

                    if (shouldReplace)
                    {
                        isNestedOk &= registry.UnregisterIsland(2);
                        isNestedOk &= registry.RegisterIsland(
                            2, secondSpots, assignments);

                        // Stop the current old-island notification as well.
                        isNestedOk &= registry.UnregisterIsland(1);
                        isNestedOk &= registry.RegisterIsland(
                            1, new[] { firstSpot }, assignments);
                    }
                };

                registry.RemainingChanged += (island, index, remaining) =>
                {
                    observedIslands.Add(island);
                    observedRemaining.Add(remaining);
                };

                isOk &= registry.TryCommit(first);
                isOk &= isNestedOk;

                if (shouldReplace)
                {
                    isOk &= observedIslands.Count == 0;
                    isOk &= registry.TryGetSpot(firstId, out SpotInfo info)
                        && info.Remaining == 3;
                    isOk &= registry.TryGetSpot(secondId, out info)
                        && info.Remaining == 3;
                    isOk &= !registry.TryCommit(first)
                        && !registry.TryCommit(second);

                    isOk &= registry.TryReserve(
                        secondId, 1, out HarvestReservation fresh);
                    isOk &= registry.TryCommit(fresh);
                    isOk &= observedIslands.Count == 1
                        && observedIslands[0] == 2
                        && observedRemaining[0] == 2;
                }
                else
                {
                    isOk &= observedIslands.Count == 2
                        && observedIslands[0] == 1
                        && observedIslands[1] == 2
                        && observedRemaining[0] == 2
                        && observedRemaining[1] == 2;
                }
            }
            finally
            {
                registry.UnregisterIsland(1);
                registry.UnregisterIsland(2);
                DestroyRoot(firstRoot);
                DestroyRoot(secondRoot);
            }

            HarvestDepositChecks.Report(
                shouldReplace
                    ? "Notification registration lifetime"
                    : "Notification cross-island order",
                isOk,
                shouldReplace
                    ? "queued and in-flight old notifications rejected"
                    : "outer notification before nested island");
            return isOk;
        }

        private static void DestroyRoot(GameObject root)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
