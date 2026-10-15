namespace Lighthouse.World.Harvest.Rules
{
    public readonly struct ConsumeResult
    {
        public readonly int ResourceId;
        public readonly int Granted;

        public ConsumeResult(int resourceId, int granted)
        {
            ResourceId = resourceId;
            Granted = granted;
        }
    }
}
