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
        [SerializeField] private float _dragCoefficient = 2f;

        public float MaxDepth => _maxDepth;

        public float GetUpwardForce(float submergedDepth, float perPointMass, float gravity)
        {
            return perPointMass * gravity * _strength * submergedDepth / _referenceDepth;
        }

        public Vector3 GetDragForce(Vector3 pointVelocity, float perPointMass)
        {
            return -pointVelocity * (perPointMass * _dragCoefficient);
        }
    }
}
