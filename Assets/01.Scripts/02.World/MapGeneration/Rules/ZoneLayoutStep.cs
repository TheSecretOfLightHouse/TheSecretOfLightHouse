using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class ZoneLayoutStep : IMapGenerationStep
    {
        public int Order => 100;

        public bool IsValidation => false;

        public void Execute(MapContext context)
        {
            IslandArchetype lighthouse = context.Config.LighthouseIsland;
            context.Lighthouse = new IslandSlot(Vector2.zero, SeaZone.None, lighthouse != null ? lighthouse.Id : 0);

            context.Zones.Clear();
            for (int i = 0; i < context.Config.Zones.Count; i++)
            {
                context.Zones.Add(new ZoneRing(context.Config.Zones[i], context.Config.RadiusScale));
            }
        }
    }
}
