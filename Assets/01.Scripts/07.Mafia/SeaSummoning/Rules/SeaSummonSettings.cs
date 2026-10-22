using Lighthouse.Monsters.Spawning.Rules;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    [CreateAssetMenu(fileName = "SeaSummonSettings", menuName = "TheLightHouse/Mafia/Sea Summon Settings")]
    public sealed class SeaSummonSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _dailyLimit = 3;
        [SerializeField, Min(1f)] private float _topViewHeight = 30f;
        [SerializeField, Min(0.01f)] private float _cameraTransitionSeconds = 0.6f;
        [SerializeField, Range(1f, 179f)] private float _topViewFieldOfView = 60f;
        [SerializeField] private MonsterCatalog _catalog;
        [SerializeField] private SeaSummonOption[] _options =
        {
            new("shark", "상어", true), new("marline", "청새치", true),
            new("manta", "가오리", true), new("angler", "아귀", true),
            new("spinocroc", "스피노크록", false), new("turtle", "버블거북", true),
            new("mosawhale", "모사웨일", false)
        };

        public int DailyLimit => _dailyLimit;
        public float TopViewHeight => _topViewHeight;
        public float CameraTransitionSeconds => _cameraTransitionSeconds;
        public float TopViewFieldOfView => _topViewFieldOfView;
        public MonsterCatalog Catalog => _catalog;
        public IReadOnlyList<SeaSummonOption> Options => _options;

        public bool TryResolveEnabled(string id, out MonsterCatalogEntry entry)
        {
            entry = null;
            if (_options == null || _catalog == null)
            {
                return false;
            }
            foreach (SeaSummonOption option in _options)
            {
                if (option != null && option.IsEnabled && option.MonsterId == id)
                {
                    return _catalog.TryResolve(id, out entry, out _);
                }
            }
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (_dailyLimit < 1 || float.IsNaN(_topViewHeight) || float.IsInfinity(_topViewHeight)
                || _topViewHeight < 1f || float.IsNaN(_cameraTransitionSeconds)
                || float.IsInfinity(_cameraTransitionSeconds) || _cameraTransitionSeconds <= 0f
                || float.IsNaN(_topViewFieldOfView) || _topViewFieldOfView < 1f || _topViewFieldOfView > 179f)
            {
                error = "하루 횟수는 1 이상, 탑뷰 높이는 유한한 1 이상, 카메라 전환 시간은 유한한 양수여야 합니다.";
                return false;
            }
            if (_catalog == null)
            {
                error = "바다 몬스터 목록을 연결해 주세요.";
                return false;
            }
            if (!_catalog.TryValidate(out error))
            {
                return false;
            }
            if (_options == null || _options.Length != 7)
            {
                error = "소환 메뉴는 7개 항목이 필요합니다.";
                return false;
            }
            for (int i = 0; i < _options.Length; i++)
            {
                SeaSummonOption option = _options[i];
                if (option == null || string.IsNullOrWhiteSpace(option.MonsterId)
                    || string.IsNullOrWhiteSpace(option.DisplayName))
                {
                    error = "소환 항목의 ID와 표시 이름이 필요합니다.";
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    if (_options[j].MonsterId == option.MonsterId)
                    {
                        error = "중복된 소환 항목 ID입니다.";
                        return false;
                    }
                }
                if (option.IsEnabled && !_catalog.TryResolve(option.MonsterId, out _, out error))
                {
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
