namespace Lighthouse.World.Debris.Rules
{
    public enum ShoreFailure : byte
    {
        None = 0,
        NoCandidateIsland = 1,
        ShoreNotFound = 2,
        UnsafeSpot = 3,
        ServiceUnavailable = 4
    }
}
