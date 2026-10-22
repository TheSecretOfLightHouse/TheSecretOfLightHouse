namespace Lighthouse.World.Islands.Rules
{
    public interface IGroundHeightSource
    {
        bool TryGetGroundHeight(short islandIndex, float x, float z, out float height);
    }
}
