using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class RightingModel
    {
        private const float MinSinAngle = 0.0001f;

        public static Vector3 GetTorque(Vector3 up, Vector3 forward, float stiffness, float mass, float gravity)
        {
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            float sinAngle = axis.magnitude;

            if (sinAngle < MinSinAngle)
            {
                if (up.y > 0f)
                {
                    return Vector3.zero;
                }

                axis = forward;
            }
            else
            {
                axis /= sinAngle;
            }

            float angle = Vector3.Angle(up, Vector3.up) * Mathf.Deg2Rad;
            return axis * (angle * stiffness * mass * gravity);
        }
    }
}
