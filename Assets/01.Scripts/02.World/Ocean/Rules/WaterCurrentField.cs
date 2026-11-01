using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public sealed class WaterCurrentField : IWaterCurrentSource
    {
        private static readonly Vector4 OpenSeaBasis = new Vector4(1f, 0f, 0f, 1f);

        private readonly Vector4[] _basis;
        private readonly int _size;
        private readonly Vector2 _origin;
        private readonly float _cell;
        private Vector2 _drive;
        private float _maxSpeed;

        public WaterCurrentField(Vector4[] basis, int size, Vector2 origin, float cell, float maxSpeed)
        {
            _basis = basis;
            _size = size;
            _origin = origin;
            _cell = cell;
            _maxSpeed = Mathf.Max(0f, maxSpeed);
        }

        public Vector4[] Basis => _basis;

        public int Size => _size;

        public Vector2 Origin => _origin;

        public float Extent => _size * _cell;

        public Vector2 Drive => _drive;

        public float MaxSpeed => _maxSpeed;

        public void SetDrive(Vector2 direction, float speed)
        {
            _drive = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized * speed : Vector2.zero;
        }

        public Vector2 GetCurrent(Vector3 worldPos)
        {
            return FlowAt(worldPos.x, worldPos.z, _drive);
        }

        public Vector2 FlowAt(float x, float z, Vector2 drive)
        {
            Vector4 basis = SampleBasis(x, z);
            Vector2 flow = new Vector2(
                drive.x * basis.x + drive.y * basis.z,
                drive.x * basis.y + drive.y * basis.w);
            float speed = flow.magnitude;
            return speed > _maxSpeed && speed > Mathf.Epsilon ? flow * (_maxSpeed / speed) : flow;
        }

        public Vector4 SampleBasis(float x, float z)
        {
            float u = (x - _origin.x) / _cell - 0.5f;
            float v = (z - _origin.y) / _cell - 0.5f;
            if (u < -0.5f || v < -0.5f || u > _size - 0.5f || v > _size - 0.5f)
            {
                return OpenSeaBasis;
            }

            u = Mathf.Clamp(u, 0f, _size - 1f);
            v = Mathf.Clamp(v, 0f, _size - 1f);
            int ix = Mathf.Min((int)u, _size - 2);
            int iz = Mathf.Min((int)v, _size - 2);
            float fx = u - ix;
            float fz = v - iz;
            return Vector4.Lerp(
                Vector4.Lerp(_basis[iz * _size + ix], _basis[iz * _size + ix + 1], fx),
                Vector4.Lerp(_basis[(iz + 1) * _size + ix], _basis[(iz + 1) * _size + ix + 1], fx),
                fz);
        }
    }
}
