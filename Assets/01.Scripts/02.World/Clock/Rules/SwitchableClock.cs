namespace Lighthouse.World.Clock.Rules
{
    public sealed class SwitchableClock : IClock
    {
        private IClock _source;

        public SwitchableClock(IClock source)
        {
            _source = source;
        }

        public double Now => _source.Now;

        public void Use(IClock source)
        {
            if (source == null)
            {
                return;
            }

            _source = source;
        }
    }
}
