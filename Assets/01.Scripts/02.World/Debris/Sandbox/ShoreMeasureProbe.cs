using System.Text;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    [DisallowMultipleComponent]
    public class ShoreMeasureProbe : MonoBehaviour, IIslandRootSource
    {
        private const int DirectionCount = 16;
        private const short ProbeIslandIndex = 0;

        [SerializeField] private Transform _islandRoot;
        [SerializeField] private DebrisConfig _config;
        [SerializeField] private float _radius = 30f;
        [SerializeField] private float _waterHeight;

        public bool TryGetRoot(short islandIndex, out Transform root)
        {
            root = _islandRoot;
            return root != null;
        }

        [ContextMenu("Measure Shore")]
        private void Measure()
        {
            if (_islandRoot == null || _config == null)
            {
                Debug.LogError($"[{nameof(ShoreMeasureProbe)}] Island root and config are required.");
                return;
            }

            Physics.SyncTransforms();
            var ground = new IslandGroundHeightSource(this);
            var center = new Vector2(_islandRoot.position.x, _islandRoot.position.z);
            float minHeight = _waterHeight + _config.MinHeightAboveWater;
            var builder = new StringBuilder();
            float min = float.MaxValue;
            float max = 0f;

            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / DirectionCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool isFound = ShoreLineSearch.TryFindShore(
                    ground, ProbeIslandIndex, center, direction, _radius,
                    _config.SearchStep, minHeight, _config.MissCountToExit, out float distance);

                builder.Append(i * 360 / DirectionCount).Append("deg=")
                    .Append(isFound ? distance.ToString("F1") : "none").Append(' ');

                if (isFound)
                {
                    min = Mathf.Min(min, distance);
                    max = Mathf.Max(max, distance);
                }
            }

            Debug.Log($"[{nameof(ShoreMeasureProbe)}] radius={_radius:F1} min={min:F1} max={max:F1} | {builder}");
        }
    }
}
