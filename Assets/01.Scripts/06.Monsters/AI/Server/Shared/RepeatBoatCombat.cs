using System;
using Lighthouse.Monsters.AI.Net;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(
        name: "배 전투 반복",
        story: "[Self]가 [TargetBoat]를 포기할 때까지 전투 행동을 반복한다",
        category: "흐름/등대/몬스터",
        id: "33eb8815bedc455387c09e7789c9529c")]
    public sealed class RepeatBoatCombat : Modifier
    {
        [SerializeReference] private BlackboardVariable<GameObject> _self = new BlackboardVariable<GameObject>();
        [SerializeReference] private BlackboardVariable<GameObject> _targetBoat = new BlackboardVariable<GameObject>();
        [NonSerialized] private MonsterBase _monster;
        [NonSerialized] private SeaMonsterSensor _sensor;
        [NonSerialized] private int _lastStartFrame;

        public BlackboardVariable<GameObject> Self { get => _self; set => _self = value; }
        public BlackboardVariable<GameObject> TargetBoat { get => _targetBoat; set => _targetBoat = value; }

        protected override Status OnStart()
        {
            if (Child == null || _self == null || _self.Value == null || _targetBoat == null)
            {
                return Status.Failure;
            }

            _monster = _self.Value.GetComponent<MonsterBase>();
            _sensor = _self.Value.GetComponent<SeaMonsterSensor>();
            if (!CanRun())
            {
                return Status.Failure;
            }

            return CanTrack() ? StartCombatCycle() : Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (!CanRun())
            {
                return Status.Failure;
            }
            
            if (Child.CurrentStatus == Status.Running || Child.CurrentStatus == Status.Waiting)
            {
                return Status.Waiting;
            }

            if (!CanTrack())
            {
                return Status.Success;
            }

            if (Child.CurrentStatus == Status.Failure)
            {
                return Status.Failure;
            }

            return _lastStartFrame == Time.frameCount ? Status.Running : StartCombatCycle();
        }

        private Status StartCombatCycle()
        {
            _lastStartFrame = Time.frameCount;
            Status status = StartNode(Child);
            if (status == Status.Failure)
            {
                return CanTrack() ? Status.Failure : Status.Success;
            }

            return status == Status.Success ? Status.Running : Status.Waiting;
        }

        private bool CanRun()
        {
            return _monster != null && _monster.isServer && _monster.IsInitialized && !_monster.IsDead
                && _monster.isActiveAndEnabled && _sensor != null && _sensor.isActiveAndEnabled;
        }

        private bool CanTrack()
        {
            return _sensor.CanTrack(_targetBoat.Value, _monster.Profile.MaxChaseDistance);
        }
    }
}
