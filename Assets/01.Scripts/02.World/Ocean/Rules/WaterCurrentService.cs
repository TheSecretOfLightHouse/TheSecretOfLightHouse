using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    // Background drift everywhere, plus island-local fields that bend it around land.
    public sealed class WaterCurrentService : IWaterCurrentSource
    {
        public const int MaxRegions = 32;

        private readonly WaterCurrentField[] _regions = new WaterCurrentField[MaxRegions];
        private readonly float _maxSpeed;
        private int _count;
        private Vector2 _drive;
        private WaterCurrentField _mapField;

        public void SetMapField(WaterCurrentField field)
        {
            _mapField = field;
            if (field != null) field.SetDrive(_drive, _drive.magnitude);
        }

        public void RemoveMapField(WaterCurrentField field)
        {
            if (_mapField == field) _mapField = null;
        }

        public WaterCurrentService(float maxSpeed)
        {
            _maxSpeed = Mathf.Max(0f, maxSpeed);
        }

        public int RegionCount => _count;

        public Vector2 Drive => _drive;

        public float MaxSpeed => _maxSpeed;

        public Vector4 SampleBasis(Vector3 worldPos)
        {
            if (_mapField != null) return _mapField.SampleBasis(worldPos.x, worldPos.z);
            for (int i = 0; i < _count; i++)
            {
                WaterCurrentField region = _regions[i];
                Vector2 origin = region.Origin;
                if (worldPos.x >= origin.x && worldPos.z >= origin.y
                    && worldPos.x <= origin.x + region.Extent && worldPos.z <= origin.y + region.Extent)
                {
                    return region.SampleBasis(worldPos.x, worldPos.z);
                }
            }
            return new Vector4(1f, 0f, 0f, 1f);
        }

        public void SetDrive(Vector2 direction, float speed)
        {
            _drive = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized * speed : Vector2.zero;
            if (_mapField != null) _mapField.SetDrive(direction, speed);
            for (int i = 0; i < _count; i++)
            {
                _regions[i].SetDrive(direction, speed);
            }
        }

        public bool TryAddRegion(WaterCurrentField field)
        {
            if (field == null || _count >= MaxRegions)
            {
                return false;
            }

            field.SetDrive(_drive, _drive.magnitude);
            _regions[_count++] = field;
            return true;
        }

        public bool RemoveRegion(WaterCurrentField field)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_regions[i] == field)
                {
                    _regions[i] = _regions[--_count];
                    _regions[_count] = null;
                    return true;
                }
            }

            return false;
        }

        public Vector2 GetCurrent(Vector3 worldPos)
        {
            if (F3WaveField.IsActive)
            {
                Vector4 geometry = F3WaveField.GeometryAt(F3WaveField.Geometry, F3WaveField.Layout,
                    F3WaveField.GeometrySize, worldPos.x, worldPos.z, F3WaveField.Regions, F3WaveField.RegionExtent);
                if (geometry.x <= 0f || geometry.y <= 0f || geometry.w <= 0f) return Vector2.zero;
            }
            if (_mapField != null) return Vector2.ClampMagnitude(_mapField.GetCurrent(worldPos), _maxSpeed);
            for (int i = 0; i < _count; i++)
            {
                WaterCurrentField region = _regions[i];
                Vector2 origin = region.Origin;
                float extent = region.Extent;
                if (worldPos.x >= origin.x && worldPos.z >= origin.y && worldPos.x <= origin.x + extent && worldPos.z <= origin.y + extent)
                {
                    return region.GetCurrent(worldPos);
                }
            }

            return Vector2.ClampMagnitude(_drive, _maxSpeed);
        }
    }
}
