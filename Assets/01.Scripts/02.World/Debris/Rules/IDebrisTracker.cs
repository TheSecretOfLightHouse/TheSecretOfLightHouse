namespace Lighthouse.World.Debris.Rules
{
    public interface IDebrisTracker
    {
        bool Register(IExpirableDrop drop, DebrisKind kind);
    }
}
