using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DockZone : MonoBehaviour
{
    [SerializeField]
    private Collider[] _landingSurfaces = new Collider[0];

    private BoxCollider _zoneCollider;

    public Collider[] LandingSurfaces => _landingSurfaces;

    private void Awake()
    {
        _zoneCollider = GetComponent<BoxCollider>();
    }

    private void Reset()
    {
        BoxCollider zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
    }

    public bool ContainsPosition(Vector3 position)
    {
        if (!_zoneCollider || !_zoneCollider.enabled)
        {
            return false;
        }

        Vector3 localPosition = _zoneCollider.transform.InverseTransformPoint(position) - _zoneCollider.center;

        Vector3 halfSize = _zoneCollider.size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfSize.x
            && Mathf.Abs(localPosition.y) <= halfSize.y
            && Mathf.Abs(localPosition.z) <= halfSize.z;
    }
}