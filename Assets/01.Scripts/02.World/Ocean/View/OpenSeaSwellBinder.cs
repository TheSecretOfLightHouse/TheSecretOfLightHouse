using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class OpenSeaSwellBinder : MonoBehaviour
    {
        private static readonly int SwellId = Shader.PropertyToID("_OpenSeaSwell");
        private static readonly int FlowId = Shader.PropertyToID("_OpenSeaFlow");

        [SerializeField]
        private int _seed = 20261031;
        [SerializeField, Range(0f, 0.5f)]
        private float _depth = 0.2f;
        [SerializeField, Min(1f)]
        private float _minWavelength = 120f;
        [SerializeField, Min(1f)]
        private float _maxWavelength = 320f;
        [SerializeField]
        private float _directionDegrees;
        [SerializeField, Range(0f, 90f)]
        private float _spreadDegrees = 55f;
        [SerializeField]
        private float _driftSpeed = 3f;

        private void OnEnable()
        {
            OpenSeaSwell.Configure(_seed, _depth, _minWavelength, _maxWavelength, _directionDegrees, _spreadDegrees, _driftSpeed);
            Shader.SetGlobalVectorArray(SwellId, OpenSeaSwell.TermArray);
            Shader.SetGlobalVector(FlowId, OpenSeaSwell.Flow);
        }

        private void OnDisable()
        {
            OpenSeaSwell.Disable();
            Shader.SetGlobalVector(FlowId, Vector4.zero);
        }
    }
}
