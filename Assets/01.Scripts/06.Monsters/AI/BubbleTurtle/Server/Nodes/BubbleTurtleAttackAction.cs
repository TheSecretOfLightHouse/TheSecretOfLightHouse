using System;
using Lighthouse.Monsters.AI.Net;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "거북이 물방울 발사", story: "[Self]가 [TargetBoat]에 물방울을 발사한다",
        category: "행동/등대/몬스터", id: "805da40c4e34493b8936c4c06675b289")]
    public sealed class BubbleTurtleAttackAction : MonsterAction
    {
        [NonSerialized] private BubbleTurtleAttack _attack;
        [NonSerialized] private MonsterAttackRange _range;
        [NonSerialized] private bool _hasStarted;

        protected override Status OnStart()
        {
            _hasStarted = false;

            if (!ResolveReferences())
            {
                return Status.Failure;
            }

            _attack = Self.Value.GetComponent<BubbleTurtleAttack>();
            _range = Self.Value.GetComponent<MonsterAttackRange>();

            return StepAttack();
        }

        protected override Status OnUpdate()
        {
            return StepAttack();
        }

        protected override void OnEnd()
        {
            if (_hasStarted && _attack != null && _attack.IsAttacking)
            {
                _attack.Cancel();
            }

            base.OnEnd();
        }

        private Status StepAttack()
        {
            if (!CanAct || _attack == null || !_attack.isActiveAndEnabled || !_attack.HasValidSetup
                || _range == null || !Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance))
            {
                return Status.Failure;
            }

            if (_hasStarted)
            {
                return _attack.IsAttacking ? Status.Running : _attack.HasCompleted ? Status.Success : Status.Failure;
            }

            if (!_attack.IsCooldownReady)
            {
                // 거북이는 공격 애니메이션이 끝나면 쿨다운이 남아 있어도 추적을 재개한다.
                Monster.TrySetBehaviorState(MonsterState.Chase);
                Motion.StepTowards(TargetBoat.Value.transform.position, Monster.Profile.ChaseSpeed, Motion.ChaseStoppingDistance);

                return Status.Running;
            }

            if (!_range.IsTargetInRange(TargetBoat.Value))
            {
                return Status.Success;
            }

            _hasStarted = _attack.BeginAttack(TargetBoat.Value);

            return _hasStarted ? Status.Running : Status.Failure;
        }
    }
}
