using System;
using Unity.Behavior;

namespace Lighthouse.Monsters.AI.Server
{
    [Serializable]
    [Condition(name: "배를 계속 추적할 수 있음", story: "[Self]가 [TargetBoat]를 계속 추적할 수 있다",
        category: "조건/등대/몬스터", id: "6b3e4da825d84b37a9ab4af7363c6d4c")]
    public sealed class BoatTrackableCondition : MonsterCondition
    {
        public override bool IsTrue()
        {
            return CanEvaluate && Sensor.CanTrack(TargetBoat.Value, Monster.Profile.MaxChaseDistance);
        }
    }
}
