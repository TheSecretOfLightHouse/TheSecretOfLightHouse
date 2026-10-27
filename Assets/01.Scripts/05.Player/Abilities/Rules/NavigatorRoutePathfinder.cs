using System.Collections.Generic;
using UnityEngine;

public static class NavigatorRoutePathfinder
{
    private const int MaxNodeCount = 65536;

    public static bool TryFindPath(
        Vector2 start,
        Vector2 destination,
        IReadOnlyList<NavigatorRouteObstacle> obstacles,
        float cellSize,
        float searchMargin,
        List<Vector2> path)
    {
        if (path == null)
        {
            return false;
        }

        path.Clear();

        if (obstacles == null
            || !IsFinite(start)
            || !IsFinite(destination)
            || !IsFinite(cellSize)
            || !IsFinite(searchMargin)
            || cellSize <= 0f
            || searchMargin < 0f)
        {
            return false;
        }

        for (int i = 0; i < obstacles.Count; i++)
        {
            NavigatorRouteObstacle obstacle = obstacles[i];

            if (!IsFinite(obstacle.Center)
                || !IsFinite(obstacle.Radius)
                || obstacle.Radius < 0f
                || !IsFinite(obstacle.Clearance)
                || obstacle.Clearance < 0f
                || obstacle.Clearance > obstacle.Radius)
            {
                return false;
            }
        }

        if (!IsEndpointClear(start, obstacles)
            || !IsEndpointClear(destination, obstacles))
        {
            return false;
        }

        if (IsSegmentClear(start, destination, obstacles, true, true))
        {
            path.Add(start);
            path.Add(destination);
            return true;
        }

        float padding = searchMargin + cellSize;
        Vector2 minimum = Vector2.Min(start, destination)
            - Vector2.one * padding;
        Vector2 maximum = Vector2.Max(start, destination)
            + Vector2.one * padding;

        if (!IsFinite(minimum) || !IsFinite(maximum))
        {
            return false;
        }

        double widthValue = System.Math.Ceiling(
            ((double)maximum.x - minimum.x) / cellSize) + 1d;
        double heightValue = System.Math.Ceiling(
            ((double)maximum.y - minimum.y) / cellSize) + 1d;

        if (widthValue < 1d || heightValue < 1d
            || widthValue * heightValue > MaxNodeCount)
        {
            return false;
        }

        int width = (int)widthValue;
        int height = (int)heightValue;
        int count = width * height;

        int startIndex = GetIndex(start, minimum, cellSize, width, height);
        int endIndex = GetIndex(
            destination, minimum, cellSize, width, height);

        if (startIndex == endIndex)
        {
            return false;
        }

        Vector2[] points = new Vector2[count];
        bool[] blocked = new bool[count];
        bool[] closed = new bool[count];
        float[] costs = new float[count];
        float[] priorities = new float[count];
        int[] parents = new int[count];

        for (int i = 0; i < count; i++)
        {
            int x = i % width;
            int z = i / width;

            points[i] = minimum + new Vector2(
                x * cellSize, z * cellSize);

            costs[i] = float.PositiveInfinity;
            priorities[i] = float.PositiveInfinity;
            parents[i] = -1;
        }

        // Exact endpoints avoid snapping the route onto nearby land.
        points[startIndex] = start;
        points[endIndex] = destination;

        for (int i = 0; i < count; i++)
        {
            blocked[i] = i == startIndex || i == endIndex
                ? false
                : !IsSegmentClear(points[i], points[i], obstacles);
        }

        if (blocked[startIndex] || blocked[endIndex])
        {
            return false;
        }

        var open = new SortedSet<(float Score, int Index)>();

        costs[startIndex] = 0f;
        priorities[startIndex] = Vector2.Distance(start, destination);
        open.Add((priorities[startIndex], startIndex));

        while (open.Count > 0)
        {
            var current = open.Min;
            open.Remove(current);

            int currentIndex = current.Index;

            if (currentIndex == endIndex)
            {
                BuildPath(endIndex, parents, points, path);
                return true;
            }

            closed[currentIndex] = true;

            int currentX = currentIndex % width;
            int currentZ = currentIndex / width;

            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetZ == 0)
                    {
                        continue;
                    }

                    int x = currentX + offsetX;
                    int z = currentZ + offsetZ;

                    if (x < 0 || z < 0 || x >= width || z >= height)
                    {
                        continue;
                    }

                    int nextIndex = z * width + x;

                    if (blocked[nextIndex] || closed[nextIndex])
                    {
                        continue;
                    }

                    Vector2 from = points[currentIndex];
                    Vector2 to = points[nextIndex];

                    if (!IsSegmentClear(from, to, obstacles,
                            currentIndex == startIndex, nextIndex == endIndex))
                    {
                        continue;
                    }

                    float nextCost = costs[currentIndex]
                        + Vector2.Distance(from, to);

                    if (nextCost >= costs[nextIndex])
                    {
                        continue;
                    }

                    open.Remove((priorities[nextIndex], nextIndex));

                    parents[nextIndex] = currentIndex;
                    costs[nextIndex] = nextCost;
                    priorities[nextIndex] = nextCost
                        + Vector2.Distance(to, destination);

                    open.Add((priorities[nextIndex], nextIndex));
                }
            }
        }

        return false;
    }

    private static int GetIndex(
        Vector2 position,
        Vector2 minimum,
        float cellSize,
        int width,
        int height)
    {
        int x = Mathf.Clamp(
            Mathf.RoundToInt((position.x - minimum.x) / cellSize),
            0,
            width - 1);

        int z = Mathf.Clamp(
            Mathf.RoundToInt((position.y - minimum.y) / cellSize),
            0,
            height - 1);

        return z * width + x;
    }

    private static bool IsSegmentClear(
        Vector2 from,
        Vector2 to,
        IReadOnlyList<NavigatorRouteObstacle> obstacles,
        bool allowFromClearance = false,
        bool allowToClearance = false)
    {
        Vector2 direction = to - from;
        float lengthSquared = direction.sqrMagnitude;

        for (int i = 0; i < obstacles.Count; i++)
        {
            NavigatorRouteObstacle obstacle = obstacles[i];

            float progress = lengthSquared > 0f
                ? Mathf.Clamp01(
                    Vector2.Dot(obstacle.Center - from, direction)
                    / lengthSquared)
                : 0f;

            Vector2 closest = from + direction * progress;
            float radiusSquared = obstacle.Radius * obstacle.Radius;

            float distanceSquared = (closest - obstacle.Center).sqrMagnitude;
            if (distanceSquared > radiusSquared)
            {
                continue;
            }

            float coreRadius = obstacle.Radius - obstacle.Clearance;
            if (distanceSquared <= coreRadius * coreRadius)
            {
                return false;
            }

            bool fromInside = allowFromClearance
                && (from - obstacle.Center).sqrMagnitude <= radiusSquared;
            bool toInside = allowToClearance
                && (to - obstacle.Center).sqrMagnitude <= radiusSquared;

            // Only endpoint connectors may enter the buffer, never the obstacle itself.
            if (fromInside && toInside)
            {
                continue;
            }

            if (fromInside && Vector2.Dot(direction, from - obstacle.Center) >= 0f)
            {
                continue;
            }

            if (toInside && Vector2.Dot(-direction, to - obstacle.Center) >= 0f)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsEndpointClear(
        Vector2 point,
        IReadOnlyList<NavigatorRouteObstacle> obstacles)
    {
        for (int i = 0; i < obstacles.Count; i++)
        {
            NavigatorRouteObstacle obstacle = obstacles[i];
            float coreRadius = obstacle.Radius - obstacle.Clearance;
            if ((point - obstacle.Center).sqrMagnitude <= coreRadius * coreRadius)
            {
                return false;
            }
        }

        return true;
    }

    private static void BuildPath(
        int endIndex,
        int[] parents,
        Vector2[] points,
        List<Vector2> path)
    {
        int index = endIndex;

        while (index >= 0)
        {
            path.Add(points[index]);
            index = parents[index];
        }

        path.Reverse();
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
