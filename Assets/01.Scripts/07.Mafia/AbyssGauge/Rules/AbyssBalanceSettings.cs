using UnityEngine;

namespace Lighthouse.Mafia.AbyssGauge.Rules
{
    [CreateAssetMenu(fileName = "AbyssBalanceSettings", menuName = "TheLightHouse/Mafia/Abyss Balance")]
    public sealed class AbyssBalanceSettings : ScriptableObject
    {
        [Header("하루 기본 심해 충전량")]
        [Tooltip("완료된 하루마다 아침에 한 번 추가하는 심해량입니다.")]
        [SerializeField, Min(0)] private float _dailyBaseGain;

        [Header("초당 봉인 증가량")]
        [Tooltip("(시민 -> 마피아) 지목한 상태를 1초 유지할 때 쌓이는 봉인량입니다. 대상 변경 시 해당 시민의 봉인은 모두 초기화됩니다.")]
        [SerializeField, Min(0)] private float _sealGainPerSecond;

        [Header("초당 오투표 심해 증가량")]
        [Tooltip("(시민 ->시민) 지목한 상태를 1초 유지할 때 쌓이는 심해량입니다. 대상이 바뀌어도 기존 오투표 누적량은 보존됩니다.")]
        [SerializeField, Min(0)] private float _wrongGainPerSecond;

        [Header("최소 괴물 공격력")]
        [Tooltip("괴물의 공격력 최솟값입니다. 소환 이후 공격력은 고정됩니다.")]
        [SerializeField, Min(0)] private float _minimumAttackPower;
      
        public AbyssBalance CreateSnapshot()
        {
            return new AbyssBalance(_dailyBaseGain, _sealGainPerSecond,
                _wrongGainPerSecond, _minimumAttackPower);
        }
    }
}
