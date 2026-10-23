namespace Lighthouse.World.Reefs.Rules
{
    public readonly struct ReefVariant
    {
        public readonly int PrefabIndex;
        public readonly float YawDegrees;

        public ReefVariant(int prefabIndex, float yawDegrees)
        {
            PrefabIndex = prefabIndex;
            YawDegrees = yawDegrees;
        }
    }
}
