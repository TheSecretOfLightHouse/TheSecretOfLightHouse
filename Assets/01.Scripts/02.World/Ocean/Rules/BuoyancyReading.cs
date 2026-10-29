namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct BuoyancyReading
    {
        public BuoyancyReading(
            float referenceSurface,
            float submergedFraction,
            float meanDepth)
        {
            IsValid = true;
            ReferenceSurface = referenceSurface;
            SubmergedFraction = submergedFraction;
            MeanDepth = meanDepth;
        }

        public bool IsValid { get; }

        public float ReferenceSurface { get; }

        public float SubmergedFraction { get; }

        public float MeanDepth { get; }
    }
}
