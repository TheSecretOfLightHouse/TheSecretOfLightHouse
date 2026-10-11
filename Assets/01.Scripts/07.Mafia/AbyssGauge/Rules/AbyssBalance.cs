using System;

namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    public readonly struct AbyssBalance
    {
        public double DailyBaseGain { get; }
        public double SealGainPerSecond { get; }
        public double WrongGainPerSecond { get; }
        public double MinimumAttackPower { get; }

        public AbyssBalance(double dailyBaseGain, double sealGainPerSecond,
            double wrongGainPerSecond, double minimumAttackPower)
        {
            Validate(dailyBaseGain, nameof(dailyBaseGain));
            Validate(sealGainPerSecond, nameof(sealGainPerSecond));
            Validate(wrongGainPerSecond, nameof(wrongGainPerSecond));
            Validate(minimumAttackPower, nameof(minimumAttackPower));
            DailyBaseGain = dailyBaseGain;
            SealGainPerSecond = sealGainPerSecond;
            WrongGainPerSecond = wrongGainPerSecond;
            MinimumAttackPower = minimumAttackPower;
        }

        private static void Validate(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(name, "Balance values must be finite and nonnegative.");
            }
        }
    }
}
