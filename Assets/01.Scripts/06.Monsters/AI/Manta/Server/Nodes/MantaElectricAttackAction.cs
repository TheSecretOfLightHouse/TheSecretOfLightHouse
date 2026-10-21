using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Lighthouse.Monsters.AI.Server", "Lighthouse.Runtime", "MantaRiseAction")]
    [Serializable]
    [NodeDescription(name: "배 앞쪽 꼬리 전깃줄 설치", story: "[Self]가 [TargetBoat] 앞을 가로질러 꼬리로 전깃줄을 남긴다",
        category: "행동/등대/몬스터", id: "0f8ac72a2b254a95aa97ef29d412d108")]
    public sealed class MantaElectricAttackAction : MonsterAction
    {
        [NonSerialized] private MantaElectricAttack _attack;
        [NonSerialized] private GameObject _target;

        protected override Status OnStart()
        {
            _attack = null;
            if (!ResolveReferences() || TargetBoat.Value == null)
            {
                return Status.Failure;
            }
            _target = TargetBoat.Value;
            _attack = Self.Value.GetComponent<MantaElectricAttack>();
            return _attack != null && _attack.BeginAttack(_target) ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _attack == null || TargetBoat.Value != _target)
            {
                return Status.Failure;
            }
            if (_attack.IsAttacking)
            {
                return Status.Running;
            }
            // 빗나가도 공격을 마쳤으면 회복 시간을 기다리고, 취소된 공격은 행동 흐름을 종료한다.
            return _attack.HasCompleted ? Status.Success : Status.Failure;
        }

        protected override void OnEnd()
        {
            if (_attack != null)
            {
                _attack.Cancel();
            }
            base.OnEnd();
        }
    }
}
