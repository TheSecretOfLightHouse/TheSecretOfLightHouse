using UnityEngine;

namespace Lighthouse.Mafia.IdentityCheck.Rules
{
    [CreateAssetMenu(fileName = "IdentityCheckSettings", menuName = "TheLightHouse/Mafia/Identity Check")]
    public sealed class IdentityCheckSettings : ScriptableObject
    {
        [Header("정체 확인 횟수")]
        [Tooltip("시험 기본값 2회. 최종 수치는 플레이테스트로 조정")]
        [SerializeField, Min(1)] private int _maxCheckCount = 2;

        [Header("확인 성공에 필요한 투표 수")]
        [Tooltip("시험 기본값 2표. 의식 승리 조건의 2표와 별개입니다.")]
        [SerializeField, Min(1)] private int _requiredVoteCount = 2;

        public IdentityCheckConfig CreateSnapshot()
        {
            return new IdentityCheckConfig(_maxCheckCount, _requiredVoteCount);
        }
    }
}
