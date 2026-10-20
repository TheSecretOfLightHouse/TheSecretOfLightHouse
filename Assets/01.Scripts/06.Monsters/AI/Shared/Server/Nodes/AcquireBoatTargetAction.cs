using System;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "감지 범위의 배 선택", story: "[Self]가 감지한 배를 [TargetBoat]에 저장한다",
        category: "행동/등대/몬스터", id: "c045c5dac4fa4ef4916a2025d3781978")]
    public sealed class AcquireBoatTargetAction : MonsterAction
    {
        protected override Status OnStart()
        {
            if (!ResolveReferences() || !Sensor.TryAcquireTarget(Monster.Profile.DetectionRadius, out GameObject target))
            {
                return Status.Failure;
            }
            TargetBoat.Value = target;
            return Status.Success;
        }

        // 감지 행동이 병렬로 실행되는 순찰 분기를 중단하지 않도록 한다.
        protected override void OnEnd() { }
    }
}
