using UnityEngine;

namespace Lighthouse.World.Clock.Rules
{
    [CreateAssetMenu(fileName = "DayCycleConfig", menuName = "TheLightHouse/World/DayCycleConfig")]
    public class DayCycleConfig : ScriptableObject
    {
        private const int MinTotalDays = 1;
        private const float MinPhaseSeconds = 1f;
        private const float MinRatio = 0.01f;
        private const float MaxRatio = 1f;

        [SerializeField] private int _totalDays = 5;
        [SerializeField] private float _daySeconds = 300f;
        [SerializeField] private float _nightSeconds = 120f;
        [SerializeField] private float _returnWarningSeconds = 60f;
        [SerializeField] private float[] _regionUnlockRatios = { 1f / 3f, 2f / 3f };

        public int TotalDays => _totalDays;
        public float DaySeconds => _daySeconds;
        public float NightSeconds => _nightSeconds;
        public float ReturnWarningSeconds => _returnWarningSeconds;
        public float CycleSeconds => _daySeconds + _nightSeconds;

        public int RegionUnlockCount => _regionUnlockRatios != null ? _regionUnlockRatios.Length : 0;

        public float GetRegionUnlockRatio(int index)
        {
            return _regionUnlockRatios[index];
        }

        private void OnValidate()
        {
            _totalDays = Mathf.Max(MinTotalDays, _totalDays);
            _daySeconds = Mathf.Max(MinPhaseSeconds, _daySeconds);
            _nightSeconds = Mathf.Max(MinPhaseSeconds, _nightSeconds);
            _returnWarningSeconds = Mathf.Clamp(_returnWarningSeconds, 0f, _daySeconds);

            if (_regionUnlockRatios == null)
            {
                return;
            }

            for (int i = 0; i < _regionUnlockRatios.Length; i++)
            {
                _regionUnlockRatios[i] = Mathf.Clamp(_regionUnlockRatios[i], MinRatio, MaxRatio);
            }
        }
    }
}
