using System;
using Lighthouse.Monsters.AI.Net;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    public abstract class MonsterAction : Unity.Behavior.Action
    {
        [SerializeReference] private BlackboardVariable<GameObject> _self = new BlackboardVariable<GameObject>();
        [SerializeReference] private BlackboardVariable<GameObject> _targetBoat = new BlackboardVariable<GameObject>();

        [NonSerialized] private MonsterBase _monster;
        [NonSerialized] private MonsterMotion _motion;
        [NonSerialized] private SeaMonsterSensor _sensor;
        [NonSerialized] private GameObject _resolvedSelf;
        [NonSerialized] private StraightAttack _attack;

        // Behavior 그래프 생성 과정에서 공개 프로퍼티를 비공개 직렬화 변수에 연결한다.
        public BlackboardVariable<GameObject> Self { get => _self; set => _self = value; }
        public BlackboardVariable<GameObject> TargetBoat { get => _targetBoat; set => _targetBoat = value; }

        protected MonsterBase Monster => _monster;
        protected MonsterMotion Motion => _motion;
        protected SeaMonsterSensor Sensor => _sensor;
        protected StraightAttack Attack => _attack;
        protected bool CanAct => _self != null && _self.Value != null
            && _targetBoat != null && _monster != null && _motion != null && _sensor != null
            && _motion.CanMove && _motion.HasValidSetup && _sensor.isActiveAndEnabled;

        protected bool ResolveReferences()
        {
            if (_self == null || _self.Value == null || _targetBoat == null)
            {
                return false;
            }

            if (_resolvedSelf != _self.Value || _monster == null || _motion == null || _sensor == null)
            {
                _resolvedSelf = _self.Value;
                _monster = _resolvedSelf.GetComponent<MonsterBase>();
                _motion = _resolvedSelf.GetComponent<MonsterMotion>();
                _sensor = _resolvedSelf.GetComponent<SeaMonsterSensor>();
                _attack = _resolvedSelf.GetComponent<StraightAttack>();
            }
            return CanAct;
        }

        protected override void OnEnd()
        {
            if (_motion != null)
            {
                _motion.Stop();
            }
        }
    }
}
