using Lighthouse.World.Ocean.Rules;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    // Re-blends the baked direction slices when the incoming wave direction drifts, then commits the finished field in one copy.
    public sealed class F3DirectionDriver
    {
        private readonly F3DirectionBlender _blender;
        private readonly NativeArray<Vector4> _staging;
        private readonly int _count;
        private readonly float _baseAngle;
        private readonly int _cellsPerFrame;
        private readonly float _retargetDegrees;
        private float _pendingAngle;

        public F3DirectionDriver(NativeArray<byte> bytes, int size, int count, float baseAngle, int cellsPerFrame, float retargetDegrees)
        {
            _blender = new F3DirectionBlender(bytes, size, count);
            _staging = new NativeArray<Vector4>(_blender.CellCount, Allocator.Persistent);
            _count = count;
            _baseAngle = baseAngle;
            _cellsPerFrame = cellsPerFrame;
            _retargetDegrees = retargetDegrees;
            AppliedAngle = float.NaN;
        }

        public float AppliedAngle { get; private set; }

        public bool IsBlending => _blender.IsBusy;

        public bool HasApplied => !float.IsNaN(AppliedAngle);

        // Returns true on the call that copied a finished blend into cells.
        public bool Tick(float targetAngle, NativeArray<Vector4> cells)
        {
            if (!_blender.IsBusy)
            {
                if (HasApplied && Mathf.Abs(Mathf.DeltaAngle(targetAngle, AppliedAngle)) < _retargetDegrees)
                {
                    return false;
                }

                Begin(targetAngle);
            }

            return _blender.Step(_staging, _cellsPerFrame) && Commit(cells);
        }

        public void ApplyImmediately(float angle, NativeArray<Vector4> cells)
        {
            Begin(angle);
            _blender.Step(_staging, _blender.CellCount);
            Commit(cells);
        }

        public void Dispose()
        {
            if (_staging.IsCreated)
            {
                _staging.Dispose();
            }
        }

        private void Begin(float angle)
        {
            F3DirectionalTransmission.PickSlices(angle, _baseAngle, _count, out int first, out int second, out float weight);
            _pendingAngle = angle;
            _blender.Begin(first, second, weight);
        }

        private bool Commit(NativeArray<Vector4> cells)
        {
            NativeArray<Vector4>.Copy(_staging, cells);
            AppliedAngle = _pendingAngle;
            return true;
        }
    }
}
