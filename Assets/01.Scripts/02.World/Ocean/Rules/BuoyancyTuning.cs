using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public class BuoyancyTuning
    {
        [SerializeField] private float _strength = 2f;
        [SerializeField] private float _referenceDepth = 1f;
        [SerializeField] private float _maxDepth = 2f;
        [SerializeField] private float _verticalDrag = 5f;
        [SerializeField] private float _horizontalDrag = 0.3f;
        [SerializeField] private float _angularDrag = 0.3f;

        public float MaxDepth => _maxDepth;

        public float GetUpwardForce(float submergedDepth, float perPointMass, float gravity)
        {
            return perPointMass * gravity * _strength * submergedDepth / _referenceDepth;
        }

        public Vector3 GetDragForce(Vector3 pointVelocity, float perPointMass)
        {
            Vector3 drag = new Vector3(
                pointVelocity.x * _horizontalDrag,
                pointVelocity.y * _verticalDrag,
                pointVelocity.z * _horizontalDrag);

            return -drag * perPointMass;
        }

        public Vector3 GetAngularDragTorque(Vector3 angularVelocity, float mass)
        {
            return -angularVelocity * (mass * _angularDrag);
        }
    }
}
