using UnityEngine;
using UnityEngine.Serialization;

namespace Lighthouse.World.Ocean.Rules
{
    // Baked in prefab coordinates at the main scene's unmodified sea level.
    public sealed class F3IslandData : ScriptableObject
    {
        [SerializeField, FormerlySerializedAs("Prefab")] private GameObject _prefab;
        [SerializeField, FormerlySerializedAs("Geometry")] private Texture2D _geometry;
        [SerializeField, FormerlySerializedAs("Coverage")] private Texture2D _coverage;
        [SerializeField, FormerlySerializedAs("Directions")] private TextAsset _directions;
        [SerializeField, FormerlySerializedAs("Origin")] private Vector2 _origin;
        [SerializeField, FormerlySerializedAs("CellSize")] private float _cellSize;

        public GameObject Prefab => _prefab;
        public Texture2D Geometry => _geometry;
        public Texture2D Coverage => _coverage;
        public TextAsset Directions => _directions;
        public Vector2 Origin => _origin;
        public float CellSize => _cellSize;
        public bool IsValid => _prefab != null && _geometry != null && _coverage != null
            && _directions != null && _cellSize > 0f && _geometry.width == _geometry.height
            && _coverage.width == _geometry.width && _coverage.height == _geometry.height;

        public void Initialize(GameObject prefab, Texture2D geometry, Texture2D coverage,
            TextAsset directions, Vector2 origin, float cellSize)
        {
            _prefab = prefab;
            _geometry = geometry;
            _coverage = coverage;
            _directions = directions;
            _origin = origin;
            _cellSize = cellSize;
        }
    }
}
