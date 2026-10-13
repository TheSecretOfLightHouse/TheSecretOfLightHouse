using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    public sealed class SeaMonsterTestTarget : MonoBehaviour
    {
        [Header("시험용 배 상태")]
        [SerializeField] private bool _isInsideIsland;

        public bool IsInsideIsland => _isInsideIsland;
        public bool IsEligible => isActiveAndEnabled && !_isInsideIsland
            && MonsterMotion.IsFinite(transform.position);
    }
}
