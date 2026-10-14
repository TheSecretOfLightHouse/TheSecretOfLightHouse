using System;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [Serializable]
    public struct DepositSpec
    {
        [SerializeField] private int _initialAmount;
        [SerializeField] private int _maxAmount;
        [SerializeField, Range(0f, 1f)] private float _dailyRegenRate;

        public DepositSpec(int initialAmount, int maxAmount, float dailyRegenRate)
        {
            _initialAmount = initialAmount;
            _maxAmount = maxAmount;
            _dailyRegenRate = dailyRegenRate;
        }

        public int InitialAmount => _initialAmount;

        public int MaxAmount => _maxAmount;

        public float DailyRegenRate => _dailyRegenRate;

        public bool IsValid => _initialAmount >= 0 && _maxAmount > 0 && _initialAmount <= _maxAmount;
    }
}
