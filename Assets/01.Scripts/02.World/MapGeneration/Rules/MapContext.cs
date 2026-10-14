using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class MapContext
    {
        private readonly List<string> _issues = new List<string>();

        public MapContext(MapGenerationConfig config, int seed)
        {
            Config = config;
            Seed = seed;
            Root = new SeededRandom(seed);
        }

        public MapGenerationConfig Config { get; }

        public int Seed { get; }

        public SeededRandom Root { get; }

        public IslandSlot Lighthouse { get; set; }

        public List<ZoneRing> Zones { get; } = new List<ZoneRing>();

        public List<IslandSlot> Slots { get; } = new List<IslandSlot>();

        public List<ReefPlacement> Reefs { get; } = new List<ReefPlacement>();

        public IReadOnlyList<string> Issues => _issues;

        public SeededRandom Fork(MapGenerationStepId stepId)
        {
            return Root.Fork((int)stepId);
        }

        public void AddIssue(string issue)
        {
            _issues.Add(issue);
        }

        public bool TryGetRadius(IslandSlot slot, out float radius)
        {
            if (slot != null && Config.TryGetArchetype(slot.ArchetypeId, out IslandArchetype archetype))
            {
                radius = archetype.Radius;
                return true;
            }

            radius = 0f;
            return false;
        }
    }
}
