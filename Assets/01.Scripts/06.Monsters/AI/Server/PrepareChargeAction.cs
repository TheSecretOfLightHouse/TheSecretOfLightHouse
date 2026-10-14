using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "돌진 공격 준비", story: "[Self]가 멈춰서 [TargetBoat]의 마지막 범위 안 위치로 돌진을 준비한다",
        category: "행동/등대/몬스터", id: "1f5629e394784824ade864d1513573a1")]
    public sealed class PrepareChargeAction : MonsterAction
    {
        [NonSerialized] private SeaMonsterChargeAttack _attack;
        [NonSerialized] private bool _completed;

        protected override Status OnStart()
        {
            _completed = false;
            if (!ResolveReferences())
            {
                return Status.Failure;
            }

            _attack = Self.Value.GetComponent<SeaMonsterChargeAttack>();
            return _attack != null && _attack.BeginPreparation(TargetBoat.Value)
                ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _attack == null || !_attack.CanAct
                || !Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance))
            {
                return Status.Failure;
            }

            _completed = _attack.StepPreparation(TargetBoat.Value, UnityEngine.Time.deltaTime);
            return _completed ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            base.OnEnd();
            if (!_completed && _attack != null)
            {
                _attack.Cancel();
            }
        }
    }
}
