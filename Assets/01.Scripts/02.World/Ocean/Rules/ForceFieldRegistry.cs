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
            Vector3 total = Vector3.zero;

            for (int i = 0; i < _staticFields.Count; i++)
            {
                IForceField field = _staticFields[i];
                if (field.TryGetBounds(out Rect bounds) && !bounds.Contains(new Vector2(worldPos.x, worldPos.z)))
                {
                    continue;
                }

                total += GetForce(field, worldPos, velocity, sailArea, time);
            }

            for (int i = 0; i < _dynamicFields.Count; i++)
            {
                total += GetForce(_dynamicFields[i], worldPos, velocity, sailArea, time);
            }

            return total;
        }

        private static Vector3 GetForce(IForceField field, Vector3 worldPos, Vector3 velocity, float sailArea, double time)
        {
            return field.IsInfluencing(worldPos) ? field.ForceAt(worldPos, velocity, sailArea, time) : Vector3.zero;
        }
    }
}
