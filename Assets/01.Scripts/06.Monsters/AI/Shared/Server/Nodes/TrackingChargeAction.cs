using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "배를 추적하며 돌진 공격",
        story: "[Self]가 [TargetBoat]를 실시간 추적하며 돌진해 한 번 공격한다",
        category: "행동/등대/몬스터", id: "0fa431ebac284ecbb26b0bc41fe4ed5a")]
    public sealed class TrackingChargeAction : MonsterAction
    {
        [NonSerialized] private TrackingAttack _trackingAttack;
        [NonSerialized] private GameObject _attackTarget;
        [NonSerialized] private bool _hasStartedAttack;

        protected override Status OnStart()
        {
            _trackingAttack = null;
            _attackTarget = null;
            _hasStartedAttack = false;
            if (!ResolveReferences() || TargetBoat.Value == null)
            {
                return Status.Failure;
            }

            _trackingAttack = Self.Value.GetComponent<TrackingAttack>();
            _attackTarget = TargetBoat.Value;
            if (_trackingAttack == null)
            {
                return Status.Failure;
            }

            // 공격 시작 중 상태 변경 콜백이 실행되어도 OnEnd에서 취소할 수 있도록 먼저 표시한다.
            _hasStartedAttack = true;
            bool started = _trackingAttack.BeginAttack(_attackTarget);
            if (!started || !_hasStartedAttack)
            {
                _hasStartedAttack = false;
                return Status.Failure;
            }
            return GetAttackStatus();
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _trackingAttack == null || !_trackingAttack.CanAct
                || TargetBoat.Value != _attackTarget)
            {
                return Status.Failure;
            }
            return GetAttackStatus();
        }

        protected override void OnEnd()
        {
            if (_hasStartedAttack && _trackingAttack != null)
            {
                _trackingAttack.Cancel();
            }
            _hasStartedAttack = false;
            base.OnEnd();
        }

        private Status GetAttackStatus()
        {
            if (_trackingAttack.IsAttacking)
            {
                return Status.Running;
            }
            return _trackingAttack.HasSucceeded ? Status.Success : Status.Failure;
        }
    }
}
