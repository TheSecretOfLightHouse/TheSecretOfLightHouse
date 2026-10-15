namespace Lighthouse.World.MapGeneration.Rules
{
    public enum MapChecksumResult : byte
    {
        Match = 0,
        ContentMismatch = 1,
        LayoutMismatch = 2,
        BuildFailed = 3
    }
}
