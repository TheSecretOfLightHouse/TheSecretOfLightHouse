using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public sealed class SpawnerRegistry : ISpawnZoneProvider
    {
        private readonly float _cellSize;
        private readonly SpawnerPoint[] _points;
        private readonly Dictionary<Vector2Int, List<SpawnerPoint>> _cells;
        private SeaZone _unlockedZones;

        public int Count => _points.Length;
        public SeaZone UnlockedZones => _unlockedZones;

        public SpawnerRegistry(IReadOnlyList<SpawnerPoint> points, float cellSize)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (!IsFinite(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            _cellSize = cellSize;
            _points = new SpawnerPoint[points.Count];
            _cells = new Dictionary<Vector2Int, List<SpawnerPoint>>();

            for (int i = 0; i < points.Count; i++)
            {
                SpawnerPoint point = points[i];
                if (!IsValid(point) || !TryGetCell(point.Position.x, point.Position.z, out Vector2Int cell))
                {
                    throw new ArgumentException("Invalid spawner point.", nameof(points));
                }

                _points[i] = point;
                if (!_cells.TryGetValue(cell, out List<SpawnerPoint> bucket))
                {
                    bucket = new List<SpawnerPoint>();
                    _cells.Add(cell, bucket);
                }

                bucket.Add(point);
            }
        }

        public void SetUnlockedZones(SeaZone zones)
        {
            _unlockedZones = zones & SeaZone.All;
        }

        public void QuerySpawners(Vector3 center, float radius, List<SpawnerPoint> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(center.z)
                || !IsFinite(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Query values must be finite and radius non-negative.");
            }

            if (_unlockedZones == SeaZone.None || _points.Length == 0)
            {
                return;
            }

            bool hasMin = TryGetCell((double)center.x - radius, (double)center.z - radius, out Vector2Int min);
            bool hasMax = TryGetCell((double)center.x + radius, (double)center.z + radius, out Vector2Int max);
            double cellCount = hasMin && hasMax
                ? ((double)max.x - min.x + 1d) * ((double)max.y - min.y + 1d)
                : double.PositiveInfinity;

            if (cellCount > _points.Length)
            {
                AppendMatches(_points, center, radius, results);
                return;
            }

            for (long x = min.x; x <= max.x; x++)
            {
                for (long z = min.y; z <= max.y; z++)
                {
                    var cell = new Vector2Int((int)x, (int)z);
                    if (_cells.TryGetValue(cell, out List<SpawnerPoint> bucket))
                    {
                        AppendMatches(bucket, center, radius, results);
                    }
                }
            }
        }

        private void AppendMatches(IReadOnlyList<SpawnerPoint> points, Vector3 center,
            float radius, List<SpawnerPoint> results)
        {
            double sqrRadius = (double)radius * radius;
            for (int i = 0; i < points.Count; i++)
            {
                SpawnerPoint point = points[i];
                if ((_unlockedZones & point.Zone) == 0)
                {
                    continue;
                }

                double x = (double)point.Position.x - center.x;
                double y = (double)point.Position.y - center.y;
                double z = (double)point.Position.z - center.z;
                if (x * x + y * y + z * z <= sqrRadius)
                {
                    results.Add(point);
                }
            }
        }

        private bool TryGetCell(double x, double z, out Vector2Int cell)
        {
            double cellX = Math.Floor(x / _cellSize);
            double cellZ = Math.Floor(z / _cellSize);
            if (cellX < int.MinValue || cellX > int.MaxValue
                || cellZ < int.MinValue || cellZ > int.MaxValue)
            {
                cell = default;
                return false;
            }

            cell = new Vector2Int((int)cellX, (int)cellZ);
            return true;
        }

        private static bool IsValid(SpawnerPoint point)
        {
            return IsFinite(point.Position.x)
                && IsFinite(point.Position.y)
                && IsFinite(point.Position.z)
                && point.ProfileId > 0
                && (point.Zone == SeaZone.Inner || point.Zone == SeaZone.Middle || point.Zone == SeaZone.Outer);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
