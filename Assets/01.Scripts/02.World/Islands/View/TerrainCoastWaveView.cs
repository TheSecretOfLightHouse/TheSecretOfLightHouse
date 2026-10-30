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

            Vector3[] contour = TerrainCoastContour.Compute(_terrain, _seaLevel, _shoreHeight, _shoreOverlap);
            _slot = CoastWaveRegistry.Register(contour, _waveBlendWidth, default, 4f, Mathf.Max(7f, _waveBlendWidth) * 5f, _shoreLift);
            if (_slot < 0)
            {
                Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Coast wave capacity exceeded.", this);
            }
        }

        private void OnDisable()
        {
            CoastWaveRegistry.Unregister(_slot);
            _slot = -1;
        }
    }
}
