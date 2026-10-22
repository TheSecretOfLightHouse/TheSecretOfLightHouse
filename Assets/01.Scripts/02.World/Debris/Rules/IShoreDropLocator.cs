namespace Lighthouse.World.Debris.Rules
{
    public interface IShoreDropLocator
    {
        ShoreDropResult GetDropPoint(in ShoreDropRequest request);
    }
}
