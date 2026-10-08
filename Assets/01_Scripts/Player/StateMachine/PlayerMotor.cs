using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    [SerializeField]
    private float _speed = 5f;

    [SerializeField]
    private float _originAcceleration = 10f;

    private void Awake()
    {
        if (!_rb)
        {
            _rb = GetComponent<Rigidbody>();
        }
    }

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Face(Quaternion rotation)
    {
        _rb.MoveRotation(rotation);
    }

    public void Move(Vector3 direction, MoveConfig config, float fixedDt)
    {
        if(config.InputLocked)
        {
            direction = Vector3.zero;
        }

        // Vertical velocity belongs to gravity and collision response, not walking input.
        direction.y = 0f;
        Vector3 targetVelocity = direction.normalized * _speed * config.SpeedMultiplier;
        float acceleration = _originAcceleration * config.AccelerationMultiplier;
        Vector3 velocity = _rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * fixedDt);
        _rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
    }
}
