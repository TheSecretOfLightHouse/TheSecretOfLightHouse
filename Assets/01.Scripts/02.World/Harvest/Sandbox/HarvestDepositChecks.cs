using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class HarvestDepositChecks
    {
        public static bool RunAll()
        {
            bool isPassed = CheckConsume();
            isPassed &= CheckRegen();
            isPassed &= CheckPolicy();
            return isPassed;
        }

        public static void Report(string testName, bool isPassed, string detail)
        {
            string message = $"[{nameof(HarvestDepositChecks)}] {testName}: {(isPassed ? "PASS" : "FAIL")} ({detail})";
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
            List<SpotAssignment> assignments = new List<SpotAssignment>
            {
                new SpotAssignment(1, 10, 20, 0.5f),
                new SpotAssignment(2, 0, 10, 0.5f),
                default
            };

            return new ResourceDeposit(assignments);
        }

        private static bool CheckConsume()
        {
            ResourceDeposit deposit = CreateDeposit();
            bool isOk = deposit.TryConsume(0, 4, out ConsumeResult result) && result.Granted == 4 && result.ResourceId == 1;
            isOk &= deposit.TryGetRemaining(0, out int remaining) && remaining == 6;
            isOk &= deposit.TryConsume(0, 100, out result) && result.Granted == 6;
            isOk &= deposit.TryGetRemaining(0, out remaining) && remaining == 0;
            isOk &= !deposit.TryConsume(0, 1, out result) && result.Granted == 0 && result.ResourceId == 1;
            isOk &= !deposit.TryConsume(1, 5, out result);
            isOk &= !deposit.TryConsume(2, 5, out result);
            isOk &= !deposit.TryConsume(0, 0, out result);
            isOk &= !deposit.TryConsume(-1, 1, out result) && !deposit.TryConsume(3, 1, out result);
            Report("Consume", isOk, "partial, clamp to remaining, depleted, empty, unassigned, bad index");
            return isOk;
        }

        private static bool CheckRegen()
        {
            ResourceDeposit deposit = CreateDeposit();
            ProportionalRegenPolicy policy = new ProportionalRegenPolicy();
            deposit.TryConsume(0, 100, out _);

            bool isOk = deposit.Regenerate(policy, 0) == 0 && deposit.Regenerate(null, 1) == 0;
            isOk &= deposit.Regenerate(policy, 1) == 2;
            isOk &= deposit.TryGetRemaining(0, out int first) && first == 10;
            isOk &= deposit.TryGetRemaining(1, out int second) && second == 5;
            isOk &= deposit.Regenerate(policy, 5) == 2;
            isOk &= deposit.TryGetRemaining(0, out first) && first == 20;
            isOk &= deposit.TryGetRemaining(1, out second) && second == 10;
            isOk &= deposit.Regenerate(policy, 1) == 0;
            Report("Regenerate", isOk, "partial per day, clamp to max, multi-day, full, zero days, null policy");
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
            Report("RegenPolicy", isOk, "rate of max, room clamp, multi-day, zero rate");
            return isOk;
        }
    }
}
