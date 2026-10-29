using Lighthouse.Mafia.SeaSummoning.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Server
{
    public sealed class SeaSummonPointValidator : MonoBehaviour
    {
        [SerializeField] private LayerMask _landLayers;
        [SerializeField, Min(1f)] private float _landProbeHeight = 1000f;
        private readonly RaycastHit[] _landHits = new RaycastHit[32];

        public bool TryResolve(Vector3 candidate, Vector3 boatPosition, float yaw, float aspect,
            SeaSummonSettings settings, out Vector3 point, out string reason)
        {
            point = default;
            reason = "월드 조회 또는 설정 미연결";
            if (settings == null || !settings.TryValidate(out _) || _landLayers.value == 0
                || !WorldServices.TryGet<IWorldQuery>(out var world))
            {
                return false;
            }
            reason = "잘못된 좌표 또는 화면비";
            if (!IsFinite(candidate) || !IsFinite(boatPosition) || !IsFinite(yaw)
                || !IsFinite(aspect) || aspect <= 0f)
            {
                return false;
            }
            point = candidate;
            point.y = world.SampleWaterHeight(point);
            if (!IsFinite(point))
            {
                return false;
            }
            Vector3 cameraPosition = boatPosition + Vector3.up * settings.TopViewHeight;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(90f, yaw, 0f)) * (point - cameraPosition);
            float halfHeight = local.z * Mathf.Tan(settings.TopViewFieldOfView * Mathf.Deg2Rad * 0.5f);
            reason = "탑뷰 화면 범위 밖";
            float outsideX = Mathf.Max(0f, Mathf.Abs(local.x) - halfHeight * aspect);
            float outsideY = Mathf.Max(0f, Mathf.Abs(local.y) - halfHeight);
            float panRadius = settings.CameraPanRadius;
            if (local.z <= 0f || !IsFinite(halfHeight) || !IsFinite(halfHeight * aspect)
                || outsideX * outsideX + outsideY * outsideY > panRadius * panRadius)
            {
                return false;
            }
            reason = "안전지대에는 소환할 수 없음";
            if (world.IsSafeZone(point))
            {
                return false;
            }

            // 물 아래 바닥과 배·플레이어는 육지로 취급하지 않는다.
            int count = Physics.RaycastNonAlloc(new Vector3(point.x, point.y + _landProbeHeight, point.z),
                Vector3.down, _landHits, _landProbeHeight * 2f, _landLayers, QueryTriggerInteraction.Ignore);
            reason = "육지 또는 지형 조회 초과";
            if (count == _landHits.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = _landHits[i].rigidbody;
                if (body != null && (body.TryGetComponent<BoatController>(out _)
                    || body.TryGetComponent<PlayerController>(out _)))
                {
                    continue;
                }
                if (_landHits[i].point.y >= point.y - 0.01f)
                {
                    return false;
                }
            }
            reason = "선택 가능";
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
