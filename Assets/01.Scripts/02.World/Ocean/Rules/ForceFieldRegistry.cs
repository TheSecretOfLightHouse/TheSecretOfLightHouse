using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public class ForceFieldRegistry : IForceFieldProvider
    {
        private const float DefaultSailArea = 1f;

        private readonly List<IForceField> _staticFields = new List<IForceField>();
        private readonly List<IForceField> _dynamicFields = new List<IForceField>();

        public int StaticCount => _staticFields.Count;

        public int DynamicCount => _dynamicFields.Count;

        public bool Register(IForceField field, bool isDynamic)
        {
            if (field == null || _staticFields.Contains(field) || _dynamicFields.Contains(field))
            {
                return false;
            }

            (isDynamic ? _dynamicFields : _staticFields).Add(field);
            return true;
        }

        public bool Unregister(IForceField field)
        {
            return _staticFields.Remove(field) || _dynamicFields.Remove(field);
        }

        public Vector3 AccumulateForce(Vector3 worldPos, double time)
        {
            return AccumulateForce(worldPos, Vector3.zero, DefaultSailArea, time);
        }

        public Vector3 AccumulateForce(Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            return Accumulate(false, default, worldPos, velocity, sailArea, time);
        }

        public Vector3 AccumulateForce(ForceFieldRoute route, Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            return Accumulate(true, route, worldPos, velocity, sailArea, time);
        }

        private Vector3 Accumulate(bool isFiltered, ForceFieldRoute route, Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            Vector3 total = Vector3.zero;

            for (int i = 0; i < _staticFields.Count; i++)
            {
                IForceField field = _staticFields[i];
                if (isFiltered && field.Route != route)
                {
                    continue;
                }

                if (field.TryGetBounds(out Rect bounds) && !bounds.Contains(new Vector2(worldPos.x, worldPos.z)))
                {
                    continue;
                }

                total += GetForce(field, worldPos, velocity, sailArea, time);
            }

            for (int i = 0; i < _dynamicFields.Count; i++)
            {
                IForceField field = _dynamicFields[i];
                if (isFiltered && field.Route != route)
                {
                    continue;
                }

                total += GetForce(field, worldPos, velocity, sailArea, time);
            }

            return total;
        }

        private static Vector3 GetForce(IForceField field, Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            return field.IsInfluencing(worldPos) ? field.ForceAt(worldPos, velocity, sailArea, time) : Vector3.zero;
        }
    }
}
