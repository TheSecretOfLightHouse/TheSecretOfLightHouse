using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    [CreateAssetMenu(fileName = "WalkBoundaryConfig", menuName = "TheLightHouse/World/WalkBoundaryConfig")]
    public class WalkBoundaryConfig : ScriptableObject
    {
        private const float MinLength = 0.1f;

        [SerializeField] private float _cellSize = 0.5f;
        [SerializeField] private float _wadeDepth = 0.5f;
        [SerializeField] private float _marginMeters;
        [SerializeField] private float _lookaheadMeters = 0.6f;
        [SerializeField] private float _seaLevel;
        [SerializeField] private int _rowsPerTick = 16;

        public float SeaLevel => _seaLevel;
        public int RowsPerTick => _rowsPerTick;
        public float CellSize => _cellSize;
        public float WadeDepth => _wadeDepth;
        public float MarginMeters => _marginMeters;
        public float LookaheadMeters => _lookaheadMeters;

        private void OnValidate()
        {
            _cellSize = Mathf.Max(MinLength, _cellSize);
            _wadeDepth = Mathf.Max(0f, _wadeDepth);
            _marginMeters = Mathf.Max(0f, _marginMeters);
            _lookaheadMeters = Mathf.Max(MinLength, _lookaheadMeters);
            _rowsPerTick = Mathf.Max(1, _rowsPerTick);
        }
    }
}
