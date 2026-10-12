namespace Lighthouse.World.Clock.Rules
{
    public static class DayCycleTransitions
    {
        public static DayCycleSignals Detect(in DayCycleState previous, in DayCycleState next)
        {
            DayCycleSignals signals = DayCycleSignals.None;
            bool isNewDay = next.DayIndex > previous.DayIndex;

            if (isNewDay)
            {
                signals |= DayCycleSignals.DayStarted;
            }

            if (isNewDay || next.Phase != previous.Phase)
            {
                if (next.Phase == DayPhase.Dusk)
                {
                    signals |= DayCycleSignals.ReturnWarningIssued;
                }
                else if (next.Phase == DayPhase.Night)
                {
                    signals |= DayCycleSignals.NightStarted;
                }
            }

            return signals;
        }
    }
}
