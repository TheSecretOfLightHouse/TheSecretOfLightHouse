using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    public abstract class MonsterCondition : Condition
    {
        // Behavior 1.0 discovers and binds conditions through public fields only.
        [SerializeReference] public BlackboardVariable<GameObject> Self = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<GameObject> TargetBoat = new BlackboardVariable<GameObject>();
        [NonSerialized] private Lighthouse.Monsters.AI.Net.MonsterBase _monster;
        [NonSerialized] private SeaMonsterSensor _sensor;
        [NonSerialized] private SeaMonsterChargeAttack _attack;
        [NonSerialized] private GameObject _resolvedSelf;

        protected Lighthouse.Monsters.AI.Net.MonsterBase Monster => _monster;
        protected SeaMonsterSensor Sensor => _sensor;
        protected SeaMonsterChargeAttack Attack => _attack;
        protected bool CanEvaluate => Self != null && Self.Value != null && TargetBoat != null
            && _monster != null && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
            && _monster.isActiveAndEnabled && _sensor != null && _sensor.isActiveAndEnabled;

        public override void OnStart()
        {
            if (Self == null || Self.Value == null)
            {
                _resolvedSelf = null;
                _monster = null;
                _sensor = null;
                _attack = null;
                return;
            }
            if (_resolvedSelf != Self.Value || _monster == null || _sensor == null)
            {
                _resolvedSelf = Self.Value;
                _monster = _resolvedSelf.GetComponent<Lighthouse.Monsters.AI.Net.MonsterBase>();
                _sensor = _resolvedSelf.GetComponent<SeaMonsterSensor>();
                _attack = _resolvedSelf.GetComponent<SeaMonsterChargeAttack>();
            }
        }
    }
}
