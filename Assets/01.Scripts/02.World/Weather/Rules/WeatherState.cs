namespace Lighthouse.World.Weather.Rules
{
    public abstract class WeatherState
    {
        private WeatherSnapshot _target;

        protected WeatherState(WeatherType type, WeatherSnapshot target)
        {
            Type = type;
            _target = target;
        }

        public WeatherType Type { get; }

        public ref readonly WeatherSnapshot Target => ref _target;

        public virtual bool CanTransitionTo(WeatherType next)
        {
            return true;
        }
    }
}
