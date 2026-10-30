using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    // Mesh islands carry a coast contour baked in island-local space, so yaw rotation moves the waves with the island.
    [DisallowMultipleComponent]
    public sealed class IslandCoastWaveView : MonoBehaviour
    {
        [SerializeField] private Vector3[] _contour = new Vector3[0];
        [SerializeField] private float _seaLevel;
        [SerializeField, Min(0.1f)] private float _waveBlendWidth = 7f;
        [SerializeField, Min(0f)] private float _shoreLift = 0.25f;

        private int _slot = -1;

        private void OnEnable()
        {
            if (_contour == null || _contour.Length != CoastWaveRegistry.Segments)
            {
                Debug.LogError($"[{nameof(IslandCoastWaveView)}] Contour must have {CoastWaveRegistry.Segments} points.", this);
                return;
            }

            var world = new Vector3[_contour.Length];
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 point = transform.TransformPoint(_contour[i]);
                point.y = _seaLevel;
                world[i] = point;
            }

            _slot = CoastWaveRegistry.Register(world, _waveBlendWidth, default, 4f, Mathf.Max(7f, _waveBlendWidth) * 5f, _shoreLift);
            if (_slot < 0)
            {
                Debug.LogError($"[{nameof(IslandCoastWaveView)}] Coast wave capacity exceeded.", this);
            }
        }

        private void OnDisable()
        {
            CoastWaveRegistry.Unregister(_slot);
            _slot = -1;
        }
    }
}
