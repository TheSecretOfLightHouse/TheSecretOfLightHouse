using UnityEngine;

namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    [CreateAssetMenu(fileName = "AbyssBalanceSettings", menuName = "Lighthouse/Mafia/Abyss Balance")]
    public sealed class AbyssBalanceSettings : ScriptableObject
    {
        [SerializeField, Min(0)] private float _dailyBaseGain;
        [SerializeField, Min(0)] private float _sealGainPerSecond;
        [SerializeField, Min(0)] private float _wrongGainPerSecond;
        [SerializeField, Min(0)] private float _minimumAttackPower;
      
        public AbyssBalance CreateSnapshot()
        {
            return new AbyssBalance(_dailyBaseGain, _sealGainPerSecond,
                _wrongGainPerSecond, _minimumAttackPower);
        }
    }
}
