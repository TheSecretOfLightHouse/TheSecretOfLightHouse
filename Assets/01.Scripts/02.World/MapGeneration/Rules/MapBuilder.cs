using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class MapBuilder
    {
        private readonly MapGenerationConfig _config;
        private readonly IMapGenerationStep[] _steps;

        public MapBuilder(MapGenerationConfig config, IReadOnlyList<IMapGenerationStep> steps)
        {
            _config = config;
            _steps = SortByOrder(steps);
        }

        public static MapBuilder CreateDefault(MapGenerationConfig config)
        {
            return new MapBuilder(config, new IMapGenerationStep[]
            {
                new ZoneLayoutStep(),
                new IslandSlotStep(),
                new IslandAssignmentStep(),
                new IslandAppearanceStep(),
                new ReefPlacementStep(),
                new ResourceFloorValidationStep(),
                new RouteValidationStep()
            });
        }

        public MapBuildResult Build(int seed, bool runValidation)
        {
            List<string> configErrors = new List<string>();
            if (!MapGenerationConfigValidator.TryValidate(_config, configErrors))
            {
                return new MapBuildResult(null, seed, 0, configErrors);
            }

            int attempts = runValidation ? _config.MaxBuildAttempts : 1;
            MapContext context = null;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                context = Run(unchecked(seed + attempt), runValidation);
                if (context.Issues.Count == 0)
                {
                    return CreateResult(context, attempt + 1);
                }
            }

            return CreateResult(context, attempts);
        }

        private MapContext Run(int seed, bool runValidation)
        {
            MapContext context = new MapContext(_config, seed);
            for (int i = 0; i < _steps.Length; i++)
            {
                if (runValidation || !_steps[i].IsValidation)
                {
                    _steps[i].Execute(context);
                }
            }

            return context;
        }

        private static MapBuildResult CreateResult(MapContext context, int attempts)
        {
            return new MapBuildResult(MapLayoutAssembler.Create(context), context.Seed, attempts, context.Issues);
        }

        private static IMapGenerationStep[] SortByOrder(IReadOnlyList<IMapGenerationStep> steps)
        {
            IMapGenerationStep[] sorted = new IMapGenerationStep[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                int j = i;
                while (j > 0 && sorted[j - 1].Order > steps[i].Order)
                {
                    sorted[j] = sorted[j - 1];
                    j--;
                }

                sorted[j] = steps[i];
            }

            return sorted;
        }
    }
}
