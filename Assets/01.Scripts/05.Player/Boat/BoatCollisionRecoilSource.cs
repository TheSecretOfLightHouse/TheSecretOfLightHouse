using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatCollisionRecoilSource : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _recoilSpeed = 2f;
    [SerializeField, Min(0.01f)]
    private float _cooldown = 0.3f;

    public float RecoilSpeed => _recoilSpeed;
    public float Cooldown => _cooldown;
}
