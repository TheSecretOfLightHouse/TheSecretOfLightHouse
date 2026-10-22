using Lighthouse.Monsters.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    [CreateAssetMenu(fileName = "SeaSummonSettings", menuName = "TheLightHouse/Mafia/Sea Summon Settings")]
    public sealed class SeaSummonSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _dailyLimit = 3;
        [SerializeField, Min(1f)] private float _topViewHeight = 30f;
        [SerializeField, Min(0.01f)] private float _cameraTransitionSeconds = 0.6f;
        [SerializeField] private MonsterCatalog _catalog;

        public int DailyLimit => _dailyLimit;
        public float TopViewHeight => _topViewHeight;
        public float CameraTransitionSeconds => _cameraTransitionSeconds;
        public MonsterCatalog Catalog => _catalog;

        public bool TryValidate(out string error)
        {
            if (_dailyLimit < 1 || float.IsNaN(_topViewHeight) || float.IsInfinity(_topViewHeight)
                || _topViewHeight < 1f || float.IsNaN(_cameraTransitionSeconds)
                || float.IsInfinity(_cameraTransitionSeconds) || _cameraTransitionSeconds <= 0f)
            {
                error = "하루 횟수는 1 이상, 탑뷰 높이는 유한한 1 이상, 카메라 전환 시간은 유한한 양수여야 합니다.";
                return false;
            }
            if (_catalog == null)
            {
                error = "바다 몬스터 목록을 연결해 주세요.";
                return false;
            }
            return _catalog.TryValidate(out error);
        }
    }
}
