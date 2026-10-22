using System;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Testing
{
    public sealed class SeaSummonTestWorldQuery : MonoBehaviour, IWorldQuery
    {
        [SerializeField] private Vector2 _safeZoneCenter = new Vector2(-8f, 0f);
        [SerializeField, Min(0f)] private float _safeZoneRadius = 3f;
        private SafeZone _safeZone;
        public float NightThreatMultiplier => 1f;

        private void Awake()
        {
            _safeZone = new SafeZone(_safeZoneCenter, _safeZoneRadius);
            // 본편 서비스가 이미 있으면 시험 조회로 덮어쓰지 않는다.
            if (!WorldServices.TryGet<IWorldQuery>(out _))
            {
                WorldServices.Register<IWorldQuery>(this);
            }
        }

        private void OnDestroy()
        {
            WorldServices.Unregister<IWorldQuery>(this);
        }

        public float SampleWaterHeight(Vector3 worldPos)
        {
            return 0f;
        }

        public void SampleWaterHeights(ReadOnlySpan<Vector3> points, Span<float> heights)
        {
            heights.Slice(0, Math.Min(points.Length, heights.Length)).Fill(0f);
        }

        public SeaZone GetZoneAt(Vector3 worldPos)
        {
            return SeaZone.Inner;
        }

        public bool IsSafeZone(Vector3 worldPos)
        {
            return _safeZone.Contains(worldPos);
        }

        public bool TryFindNearestIsland(Vector3 from, out IslandHandle island)
        {
            island = default;
            return false;
        }
    }
}
