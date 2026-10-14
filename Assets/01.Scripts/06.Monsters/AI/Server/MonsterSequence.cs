using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [NodeDescription(name: "순찰 · 전투 · 복귀 순서 실행", story: "순찰 후 배 전투를 반복하고 시작 위치로 복귀한다",
        category: "흐름/등대/몬스터", id: "6ce5fca0a3394980a891244e95d21701")]
    public sealed class MonsterSequence : Composite
    {
        [NonSerialized] private int _childIndex;

        protected override Status OnStart()
        {
            _childIndex = 0;
            return StartCurrentChild();
        }

        protected override Status OnUpdate()
        {
            if (_childIndex >= Children.Count)
            {
                return Status.Success;
            }

            Status status = Children[_childIndex].CurrentStatus;
            if (status == Status.Success)
            {
                _childIndex++;
                return StartCurrentChild();
            }

            return status == Status.Running || status == Status.Waiting ? Status.Waiting : status;
        }

        private Status StartCurrentChild()
        {
            if (_childIndex >= Children.Count)
            {
                return Status.Success;
            }

            Status status = StartNode(Children[_childIndex]);
            if (status == Status.Success)
            {
                return _childIndex + 1 >= Children.Count ? Status.Success : Status.Running;
            }

            return status == Status.Running || status == Status.Waiting ? Status.Waiting : status;
        }
    }
}
