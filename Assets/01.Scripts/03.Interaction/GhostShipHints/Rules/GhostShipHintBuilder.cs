using System.Collections.Generic;
using Lighthouse.Workbench.Rules;

namespace Lighthouse.Interaction.GhostShipHints.Rules
{
    // Turns the workbench withdraw log into the hint (2026-10-30 decision): every withdraw so far, summed per day and
    // resource, ordered by day then resource id. An empty result means nothing has been withdrawn yet, which the
    // reader is told as it is. Runs once per reading, so the small allocations here are fine.
    public static class GhostShipHintBuilder
    {
        public static GhostShipHintEntry[] Build(IReadOnlyList<WithdrawRecord> withdrawals)
        {
            if (withdrawals == null || withdrawals.Count == 0)
            {
                return new GhostShipHintEntry[0];
            }

            List<GhostShipHintEntry> entries = new List<GhostShipHintEntry>();

            for (int i = 0; i < withdrawals.Count; i++)
            {
                WithdrawRecord record = withdrawals[i];

                if (record.Amount <= 0)
                {
                    continue;
                }

                int index = IndexOf(entries, record.Day, record.ResourceId);

                if (index < 0)
                {
                    entries.Add(new GhostShipHintEntry(record.Day, record.ResourceId, record.Amount));
                }
                else
                {
                    GhostShipHintEntry entry = entries[index];
                    entries[index] = new GhostShipHintEntry(entry.Day, entry.ResourceId, entry.Amount + record.Amount);
                }
            }

            entries.Sort(Compare);
            return entries.ToArray();
        }

        private static int IndexOf(List<GhostShipHintEntry> entries, int day, int resourceId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Day == day && entries[i].ResourceId == resourceId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int Compare(GhostShipHintEntry a, GhostShipHintEntry b)
        {
            return a.Day != b.Day ? a.Day.CompareTo(b.Day) : a.ResourceId.CompareTo(b.ResourceId);
        }
    }
}
