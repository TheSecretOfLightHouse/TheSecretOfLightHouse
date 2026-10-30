using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class DepositDistributorChecks
    {
        private const int Budgeted = 7;
        private const int Other = 9;

        public static bool RunAll()
        {
            bool isPassed = CheckSplit();
            isPassed &= CheckScarce();
            isPassed &= CheckUntouched();
            return isPassed;
        }

        private static bool CheckSplit()
        {
            List<List<SpotAssignment>> islands = BuildIslands();
            DepositDistributor.Apply(new Dictionary<int, int> { { Budgeted, 50 } }, islands);

            bool isOk = islands[0][0].InitialAmount == 17 && islands[0][2].InitialAmount == 17 && islands[1][0].InitialAmount == 16;
            isOk &= islands[0][0].MaxAmount == 17 && islands[0][0].DailyRegenRate == 0.25f;
            isOk &= islands[0][0].InitialAmount + islands[0][2].InitialAmount + islands[1][0].InitialAmount == 50;
            HarvestDepositChecks.Report("Budget split 50/3", isOk, $"{islands[0][0].InitialAmount}/{islands[0][2].InitialAmount}/{islands[1][0].InitialAmount}");
            return isOk;
        }

        private static bool CheckScarce()
        {
            List<List<SpotAssignment>> islands = BuildIslands();
            DepositDistributor.Apply(new Dictionary<int, int> { { Budgeted, 2 } }, islands);

            int sum = islands[0][0].InitialAmount + islands[0][2].InitialAmount + islands[1][0].InitialAmount;
            bool isOk = sum == 2 && islands[1][0].InitialAmount == 0;
            HarvestDepositChecks.Report("Budget smaller than spots", isOk, $"sum={sum}, last={islands[1][0].InitialAmount}");
            return isOk;
        }

        private static bool CheckUntouched()
        {
            List<List<SpotAssignment>> islands = BuildIslands();
            DepositDistributor.Apply(new Dictionary<int, int> { { Budgeted, 50 }, { 99, 10 } }, islands);

            bool isOk = islands[0][1].InitialAmount == 40 && islands[0][1].MaxAmount == 80
                && !islands[1][1].IsAssigned && islands[2].Count == 0;
            HarvestDepositChecks.Report("Budget leaves others", isOk, "other resource, unassigned spot and empty island unchanged");
            return isOk;
        }

        private static List<List<SpotAssignment>> BuildIslands()
        {
            return new List<List<SpotAssignment>>
            {
                new List<SpotAssignment>
                {
                    new SpotAssignment(Budgeted, 30, 60, 0.25f),
                    new SpotAssignment(Other, 40, 80, 0.5f),
                    new SpotAssignment(Budgeted, 10, 20, 0.25f)
                },
                new List<SpotAssignment>
                {
                    new SpotAssignment(Budgeted, 5, 10, 0.25f),
                    default
                },
                new List<SpotAssignment>()
            };
        }
    }
}
