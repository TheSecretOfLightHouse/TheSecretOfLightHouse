using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Query.Rules;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NavigatorRoutePlanner : MonoBehaviour
{
    [SerializeField]
    private MapStateNet _map;

    [SerializeField, Min(0f)]
    private float _searchMargin = 100f;

    private readonly List<NavigatorRouteObstacle> _obstacles =
        new List<NavigatorRouteObstacle>();

    private void OnEnable()
    {
        WorldServices.Register<NavigatorRoutePlanner>(this);
    }

    private void OnDisable()
    {
        WorldServices.Unregister<NavigatorRoutePlanner>(this);
    }

    public bool TryFindPath(
        Vector3 start,
        Vector3 destination,
        List<Vector2> path)
    {
        if (path == null)
        {
            return false;
        }

        path.Clear();

        if (!isActiveAndEnabled
            || !_map || !_map.Config
            || !IsFinite(start)
            || !IsFinite(destination))
        {
            return false;
        }

        if (!NavigatorRouteMapData.TryCollectObstacles(
                _map, _obstacles))
        {
            return false;
        }

        Vector2 startPosition = new Vector2(start.x, start.z);
        Vector2 destinationPosition = new Vector2(
            destination.x, destination.z);

        return NavigatorRoutePathfinder.TryFindPath(
            startPosition,
            destinationPosition,
            _obstacles,
            _map.Config.RouteCellSize,
            _searchMargin,
            path);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.z);
    }
}