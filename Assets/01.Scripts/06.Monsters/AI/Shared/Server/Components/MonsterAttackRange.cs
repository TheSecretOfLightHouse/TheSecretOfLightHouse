using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class MonsterAttackRange : MonoBehaviour, IMonsterAttackRange
    {
        [SerializeField, Min(0.01f)] private float _attackRange = 4f;

        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;

        public float AttackRange => _attackRange;
        public bool CanAct => isActiveAndEnabled && _motion != null && _motion.CanMove
            && _sensor != null && _sensor.isActiveAndEnabled
            && !float.IsNaN(_attackRange) && !float.IsInfinity(_attackRange) && _attackRange > 0f;

        private void Awake()
        {
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
        }

        public bool IsTargetInRange(GameObject target)
        {
            return CanAct && _sensor.CanTrack(target, _attackRange);
        }
    }
}
