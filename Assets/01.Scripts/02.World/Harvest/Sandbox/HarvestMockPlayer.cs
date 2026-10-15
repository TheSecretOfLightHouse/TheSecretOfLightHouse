using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Clock.Sandbox;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Sandbox
{
    public class HarvestMockPlayer : MonoBehaviour
    {
        private const double ManualStartTime = 100.0;

        [SerializeField] private GameClock _clock;
        [SerializeField] private Transform _islandRoot;
        [SerializeField] private short _islandIndex = 1;
        [SerializeField, Min(0)] private int _localIndex;
        [SerializeField, Min(1)] private int _requestAmount = 10;
        [SerializeField, Min(1f)] private float _secondsPerDay = 420f;

        private ManualClock _manual;

        [ContextMenu("Harvest/Log Spots")]
        private void LogSpots()
        {
            if (_islandRoot == null || !WorldServices.TryGet(out IHarvestSpotQuery query))
            {
                Debug.LogWarning($"[{nameof(HarvestMockPlayer)}] 섬 루트와 HarvestHost가 필요합니다.");
                return;
            }

            HarvestSpot[] spots = _islandRoot.GetComponentsInChildren<HarvestSpot>(true);
            for (int i = 0; i < spots.Length; i++)
            {
                HarvestSpot spot = spots[i];
                if (spot.IsBound && query.TryGetSpot(spot.SpotId, out SpotInfo info))
                {
                    Debug.Log($"[{nameof(HarvestMockPlayer)}] local={i} spotId={spot.SpotId} {spot.Category} resource={info.ResourceId} remaining={info.Remaining} pos={info.Position}");
                }
                else
                {
                    Debug.Log($"[{nameof(HarvestMockPlayer)}] local={i} {spot.name} {spot.Category} unassigned");
                }
            }

            Debug.Log($"[{nameof(HarvestMockPlayer)}] day={(_clock != null ? _clock.DayIndex : 0)}");
        }

        [ContextMenu("Harvest/Consume")]
        private void Consume()
        {
            if (!WorldServices.TryGet(out IHarvestSpotRegistry registry))
            {
                Debug.LogWarning($"[{nameof(HarvestMockPlayer)}] HarvestHost가 필요합니다.");
                return;
            }

            int spotId = HarvestSpotIds.Compose(_islandIndex, (ushort)_localIndex);
            bool isGranted = registry.TryConsume(spotId, _requestAmount, out ConsumeResult result);
            Debug.Log($"[{nameof(HarvestMockPlayer)}] Consume spotId={spotId} requested={_requestAmount} ok={isGranted} resource={result.ResourceId} granted={result.Granted}");
        }

        [ContextMenu("Harvest/Start Manual Clock")]
        private void StartManualClock()
        {
            if (_clock == null)
            {
                Debug.LogWarning($"[{nameof(HarvestMockPlayer)}] GameClock이 필요합니다.");
                return;
            }

            _manual = new ManualClock { Now = ManualStartTime };
            _clock.UseSource(_manual);
            _clock.BeginAt(_manual.Now);
            Debug.Log($"[{nameof(HarvestMockPlayer)}] Manual clock started, day={_clock.DayIndex}");
        }

        [ContextMenu("Harvest/Advance One Day")]
        private void AdvanceOneDay()
        {
            if (_manual == null)
            {
                Debug.LogWarning($"[{nameof(HarvestMockPlayer)}] 먼저 Start Manual Clock을 실행하세요.");
                return;
            }

            _manual.Now += _secondsPerDay;
            Debug.Log($"[{nameof(HarvestMockPlayer)}] Advanced {_secondsPerDay}s. 다음 프레임에 DayStarted가 발생합니다. Log Spots로 확인하세요.");
        }
    }
}
