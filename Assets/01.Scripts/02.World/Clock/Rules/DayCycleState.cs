namespace Lighthouse.World.Clock.Rules
{
    public readonly struct DayCycleState
    {
        public readonly int DayIndex;
        public readonly DayPhase Phase;

        public DayCycleState(int dayIndex, DayPhase phase)
        {
            DayIndex = dayIndex;
            Phase = phase;
        }
    }
}
