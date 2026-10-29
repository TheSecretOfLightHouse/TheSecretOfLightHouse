using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    // Terrain islands have no Ground_ContinuousClosed mesh for CoastalShelfView to scan.
    // Rebuild the outer shoreline once per spawn so terrain edits reach CPU and GPU together.
    [DisallowMultipleComponent]
    public sealed class TerrainCoastWaveView : MonoBehaviour
    {
        [SerializeField] private Terrain _terrain;
        [SerializeField] private float _seaLevel;
        [SerializeField, Min(0.01f)] private float _shoreHeight = 0.1f;
        [SerializeField, Min(0.1f)] private float _waveBlendWidth = 7f;
        [SerializeField, Min(0f)] private float _shoreOverlap = 1.5f;
        [SerializeField, Min(0f)] private float _shoreLift = 0.25f;

        private int _slot = -1;

        private void OnEnable()
        {
            if (!_terrain || !_terrain.terrainData)
            {
                Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Terrain is required.", this);
                return;
            }

            Vector3 origin = _terrain.transform.position;
            Vector3 size = _terrain.terrainData.size;
            Vector3 center = origin + size * 0.5f;
            center.y = _seaLevel;
            var contour = new Vector3[CoastWaveRegistry.Segments];
            float height = _seaLevel + _shoreHeight - origin.y;

            for (int i = 0; i < contour.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / contour.Length;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                float reachX = Mathf.Abs(x) > 0.0001f ? size.x * 0.5f / Mathf.Abs(x) : float.MaxValue;
                float reachZ = Mathf.Abs(z) > 0.0001f ? size.z * 0.5f / Mathf.Abs(z) : float.MaxValue;
                float reach = Mathf.Min(reachX, reachZ);
                float inside = 0f;

                // Scan inward from the boundary: an inland pond must not become the outer coast.
                for (float r = reach; r > 0f; r -= 0.1f)
                {
                    Vector3 point = center + new Vector3(x * r, 0f, z * r);
                    if (IsLand(point, height))
                    {
                        inside = r;
                        break;
                    }
                }

                float outside = Mathf.Min(inside + 0.1f, reach);
                for (int step = 0; step < 8; step++)
                {
                    float r = (inside + outside) * 0.5f;
                    Vector3 point = center + new Vector3(x * r, 0f, z * r);
                    if (IsLand(point, height))
                    {
                        inside = r;
                    }
                    else
                    {
                        outside = r;
                    }
                }

                contour[i] = center + new Vector3(x, 0f, z) * Mathf.Max(0f, inside - _shoreOverlap);
            }

            _slot = CoastWaveRegistry.Register(contour, _waveBlendWidth, default, 4f, Mathf.Max(7f, _waveBlendWidth) * 5f, _shoreLift);
            if (_slot < 0)
            {
                Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Coast wave capacity exceeded.", this);
            }
        }

        private bool IsLand(Vector3 point, float height)
        {
            TerrainData data = _terrain.terrainData;
            Vector3 local = point - _terrain.transform.position;
            int x = Mathf.Clamp((int)(local.x / data.size.x * data.holesResolution), 0, data.holesResolution - 1);
            int z = Mathf.Clamp((int)(local.z / data.size.z * data.holesResolution), 0, data.holesResolution - 1);
            return !data.IsHole(x, z) && _terrain.SampleHeight(point) > height;
        }

        private void OnDisable()
        {
            CoastWaveRegistry.Unregister(_slot);
            _slot = -1;
        }
    }
}
