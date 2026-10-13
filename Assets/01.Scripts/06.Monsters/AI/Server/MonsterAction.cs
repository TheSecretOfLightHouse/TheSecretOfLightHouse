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

        [NonSerialized] private SeaMonster _monster;
        [NonSerialized] private MonsterMotion _motion;
        [NonSerialized] private SeaMonsterTestSensor _sensor;

        // Behavior 그래프 생성 과정에서 공개 프로퍼티를 비공개 직렬화 변수에 연결한다.
        public BlackboardVariable<GameObject> Self { get => _self; set => _self = value; }
        public BlackboardVariable<GameObject> TargetBoat { get => _targetBoat; set => _targetBoat = value; }

        protected SeaMonster Monster => _monster;
        protected MonsterMotion Motion => _motion;
        protected SeaMonsterTestSensor Sensor => _sensor;
        protected bool CanAct => _self != null && _self.Value != null
            && _targetBoat != null && _monster != null && _motion != null && _sensor != null
            && _motion.CanMove && _motion.HasValidSetup && _sensor.isActiveAndEnabled;

        protected bool ResolveReferences()
        {
            if (_self == null || _self.Value == null || _targetBoat == null)
            {
                return false;
            }

            _monster = _self.Value.GetComponent<SeaMonster>();
            _motion = _self.Value.GetComponent<MonsterMotion>();
            _sensor = _self.Value.GetComponent<SeaMonsterTestSensor>();
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
