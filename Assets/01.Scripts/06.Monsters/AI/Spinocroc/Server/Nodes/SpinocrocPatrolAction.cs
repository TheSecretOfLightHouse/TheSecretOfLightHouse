using System;
using Lighthouse.Monsters.AI.Server;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Server
{
    [Serializable]
    [NodeDescription(name: "스피노크록 배회와 대상 선정", story: "[Self]가 배회하며 공격 준비 대상을 선정한다",
        category: "행동/등대/몬스터", id: "e93b54f70cf14c779b30b953d190568b")]
    public sealed class SpinocrocPatrolAction : MonsterAction
    {
        [NonSerialized] private SpinocrocPreparation _preparation;
        [NonSerialized] private float _waitUntil;
        [NonSerialized] private bool _isWaiting;

        protected override Status OnStart()
        {
            if (!ResolveReferences())
            {
                return Status.Failure;
            }
            _preparation = Self.Value.GetComponent<SpinocrocPreparation>();
            _isWaiting = false;
            Monster.TrySetBehaviorState(MonsterState.Patrol);
            return _preparation != null && _preparation.CanAct && Motion.BeginPatrolLeg()
                ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (!CanAct || _preparation == null || !_preparation.CanAct)
            {
                return Status.Failure;
            }
            if (_preparation.TryBegin())
            {
                return Status.Success;
            }
            if (_isWaiting)
            {
                if (Time.time >= _waitUntil)
                {
                    _isWaiting = false;
                    if (!Motion.BeginPatrolLeg())
                    {
                        return Status.Failure;
                    }
                }
            }
            else if (Motion.StepPatrol(Monster.Profile.PatrolSpeed))
            {
                _isWaiting = true;
                Vector2 wait = Motion.PatrolWaitRange;
                _waitUntil = Time.time + UnityEngine.Random.Range(wait.x, wait.y);
            }
            return Status.Running;
        }
    }
}
