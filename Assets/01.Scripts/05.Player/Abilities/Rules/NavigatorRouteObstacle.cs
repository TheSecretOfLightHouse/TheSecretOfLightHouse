using UnityEngine;

public readonly struct NavigatorRouteObstacle
{
    public Vector2 Center { get; }
    public float Radius { get; }
    public float Clearance { get; }

    public NavigatorRouteObstacle(Vector2 center, float radius, float clearance = 0f)
    {
        Center = center;
        Radius = radius;
        Clearance = clearance;
    }
}
