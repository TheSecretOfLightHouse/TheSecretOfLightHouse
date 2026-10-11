using System;

namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    public readonly struct AbyssGaugeSnapshot
    {
        public double RecordedAt { get; }
        public double BaseAbyss { get; }
        public double TotalSeal { get; }
        public double TotalWrong { get; }
        public double Abyss => BaseAbyss + TotalWrong;
        public double AttackPower { get; }

        public AbyssGaugeSnapshot(double recordedAt, double baseAbyss, double totalSeal,
            double totalWrong, double minimumAttackPower)
        {
            RecordedAt = recordedAt;
            BaseAbyss = baseAbyss;
            TotalSeal = totalSeal;
            TotalWrong = totalWrong;
            AttackPower = Math.Max(minimumAttackPower, baseAbyss + totalWrong - totalSeal);
        }
    }
}
