namespace Lighthouse.World.MapGeneration.Rules
{
    public interface IMapGenerationStep
    {
        int Order { get; }

        bool IsValidation { get; }

        void Execute(MapContext context);
    }
}
