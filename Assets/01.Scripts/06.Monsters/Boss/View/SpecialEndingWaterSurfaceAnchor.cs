using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20000)]
    public sealed class SpecialEndingWaterSurfaceAnchor : MonoBehaviour
    {
        [SerializeField] private Transform _horizontalTarget;
        [SerializeField] private float _surfaceOffset = 0.05f;
        [SerializeField] private bool _keepAboveSurface;
        [SerializeField] private float _authoredLocalY = -3.3f;
        [SerializeField] private bool _trackChainSurface;
        private IWorldQuery _query;
        private IClock _clock;
        private WaterSamplerHost _host;

        public bool HasSurface { get; private set; }
        public float SurfaceHeight { get; private set; }
        public bool HasContact { get; private set; }

        private void LateUpdate()
        {
            Vector3 point = _horizontalTarget != null ? _horizontalTarget.position : transform.position;
            HasContact = !_trackChainSurface || TryFindContact(out point);
            HasSurface = TrySample(point, out float height);
            if (!HasSurface)
            {
                return;
            }
            SurfaceHeight = height;
            if (_keepAboveSurface)
            {
                // Rebuild from the authored camera height so a paused Timeline never accumulates offsets.
                Vector3 local = transform.localPosition;
                local.y = _authoredLocalY;
                float authored = transform.parent != null ? transform.parent.TransformPoint(local).y : local.y;
                point.y = Mathf.Max(authored, height + _surfaceOffset);
            }
            else
            {
                point.y = height + _surfaceOffset;
            }
            transform.position = point;
        }

        private bool TryFindContact(out Vector3 point)
        {
            point = _horizontalTarget != null ? _horizontalTarget.position : transform.position;
            Transform tip = _horizontalTarget;
            for (int i = 0; i < 14 && tip != null && tip.parent != null; i++, tip = tip.parent)
            {
                Vector3 a = tip.position;
                Vector3 b = tip.parent.position;
                if (!TrySample(a, out float waterA) || !TrySample(b, out float waterB))
                {
                    return false;
                }
                float side = a.y - waterA;
                if (side * (b.y - waterB) > 0f)
                {
                    continue;
                }
                // Locate the moving chain's crossing instead of emitting foam under an airborne tip.
                for (int j = 0; j < 8; j++)
                {
                    Vector3 middle = (a + b) * 0.5f;
                    if (!TrySample(middle, out float water))
                    {
                        return false;
                    }
                    if ((middle.y - water) * side > 0f)
                    {
                        a = middle;
                    }
                    else
                    {
                        b = middle;
                    }
                }
                point = (a + b) * 0.5f;
                return true;
            }
            return false;
        }

        private bool TrySample(Vector3 point, out float height)
        {
            if (_query == null)
            {
                WorldServices.TryGet(out _query);
            }
            if (_query != null)
            {
                height = _query.SampleWaterHeight(point);
            }
            else
            {
                if (_host == null)
                {
                    WorldServices.TryGet(out _host);
                }
                if (_clock == null)
                {
                    WorldServices.TryGet(out _clock);
                }
                if (_host == null || _clock == null)
                {
                    height = 0f;
                    return false;
                }
                height = WaveModel.HeightAt(_host.Inputs.Waves, _host.Inputs.Modifiers,
                    CoastWaveRegistry.Bounds, CoastWaveRegistry.Edges, CoastWaveRegistry.Profiles,
                    point.x, point.z, (float)_clock.Now);
            }
            return !float.IsNaN(height) && !float.IsInfinity(height);
        }
    }
}
