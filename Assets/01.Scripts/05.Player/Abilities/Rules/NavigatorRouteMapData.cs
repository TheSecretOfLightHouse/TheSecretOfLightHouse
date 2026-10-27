using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using System.Collections.Generic;
using UnityEngine;
public static class NavigatorRouteMapData
{
    public static bool TryCollectObstacles(
        MapStateNet map,
        List<NavigatorRouteObstacle> obstacles)
    {
        if (obstacles == null)
        {
            return false;
        }

        obstacles.Clear();

        if (!map || !map.isActiveAndEnabled
            || !map.IsLayoutReady
            || map.Layout == null
            || !map.Config)
        {
            return false;
        }

        float clearance = map.Config.ShipClearance;

        if (float.IsNaN(clearance)
            || float.IsInfinity(clearance)
            || clearance < 0f)
        {
            return false;
        }

        MapLayout layout = map.Layout;

        for (int i = 0; i < layout.Islands.Count; i++)
        {
            IslandPlacement island = layout.Islands[i];

            obstacles.Add(new NavigatorRouteObstacle(
                island.Position,
                island.Radius + clearance,
                clearance));
        }

        for (int i = 0; i < layout.Reefs.Count; i++)
        {
            ReefPlacement reef = layout.Reefs[i];

            obstacles.Add(new NavigatorRouteObstacle(
                reef.Position,
                reef.Radius + clearance,
                clearance));
        }

        return true;
    }
}
