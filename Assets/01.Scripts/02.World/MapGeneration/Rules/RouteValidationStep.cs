using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class RouteValidationStep : IMapGenerationStep
    {
        private const float ReachCells = 2f;
        private const float ExtentCells = 3f;

        public int Order => 900;

        public bool IsValidation => true;

        public void Execute(MapContext context)
        {
            float clearance = context.Config.ShipClearance;
            float cell = context.Config.RouteCellSize;
            float reach = cell * ReachCells;
            RouteGrid grid = new RouteGrid(CalculateHalfExtent(context, clearance, cell), cell);

            BlockObstacles(context, grid, clearance);
            context.TryGetRadius(context.Lighthouse, out float lighthouseRadius);
            grid.FloodFromRing(context.Lighthouse.Position, lighthouseRadius + clearance, lighthouseRadius + clearance + reach);

            for (int i = 0; i < context.Slots.Count; i++)
            {
                IslandSlot slot = context.Slots[i];
                if (context.TryGetRadius(slot, out float radius) && !grid.IsReachedNear(slot.Position, radius + clearance + reach))
                {
                    context.AddIssue($"{slot.Zone} 섬({slot.Position.x:F0}, {slot.Position.y:F0})까지 항로가 없음");
                }
            }
        }

        private static void BlockObstacles(MapContext context, RouteGrid grid, float clearance)
        {
            if (context.TryGetRadius(context.Lighthouse, out float lighthouseRadius))
            {
                grid.BlockCircle(context.Lighthouse.Position, lighthouseRadius + clearance);
            }

            for (int i = 0; i < context.Slots.Count; i++)
            {
                if (context.TryGetRadius(context.Slots[i], out float radius))
                {
                    grid.BlockCircle(context.Slots[i].Position, radius + clearance);
                }
            }

            for (int i = 0; i < context.Reefs.Count; i++)
            {
                grid.BlockCircle(context.Reefs[i].Position, context.Reefs[i].Radius + clearance);
            }
        }

        private static float CalculateHalfExtent(MapContext context, float clearance, float cell)
        {
            float maxOuter = 0f;
            for (int i = 0; i < context.Zones.Count; i++)
            {
                maxOuter = Mathf.Max(maxOuter, context.Zones[i].OuterRadius);
            }

            float maxRadius = 0f;
            for (int i = 0; i < context.Slots.Count; i++)
            {
                if (context.TryGetRadius(context.Slots[i], out float radius))
                {
                    maxRadius = Mathf.Max(maxRadius, radius);
                }
            }

            return maxOuter + maxRadius + clearance + cell * ExtentCells;
        }
    }
}
