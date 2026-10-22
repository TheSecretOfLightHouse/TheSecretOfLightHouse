using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class BoatContactBoarding : MonoBehaviour
{
    private const int QueryCapacity = 32;
    private const int LandingDirectionCount = 8;
    private const float DirectionEpsilon = 0.0001f;

    [SerializeField]
    private BoatController _boat;
    [SerializeField]
    private LayerMask _landLayers;
    [SerializeField]
    private Collider[] _landColliders = new Collider[0];
    [SerializeField, Min(0.01f)]
    private float _contactTolerance = 0.15f;
    [SerializeField, Min(0.01f)]
    private float _landingClearance = 0.1f;
    [SerializeField, Min(0.01f)]
    private float _landingProbeHeight = 3f;
    [SerializeField, Min(0.01f)]
    private float _maxLandingDistance = 3f;
    [SerializeField, Range(0f, 89f)]
    private float _maxLandingSlope = 45f;

    private readonly Collider[] _lands = new Collider[QueryCapacity];
    private readonly Collider[] _obstacles = new Collider[QueryCapacity];
    private readonly Collider[] _dockCandidates = new Collider[QueryCapacity];
    private BoxCollider _body;
    private readonly Collider[] _departureLands = new Collider[QueryCapacity];
    private int _departureLandCount;

    public void ConfigureLandLayers(LayerMask landLayers)
    {
        _landLayers = landLayers;
    }

    public void BeginDepartureProtection()
    {
        _departureLandCount = 0;
        int count = CollectLand();
        for (int i = 0; i < count; i++)
        {
            if (IsTouchingLand(_lands[i]))
            {
                _departureLands[_departureLandCount++] = _lands[i];
            }
        }
    }

    public void RefreshDepartureContacts()
    {
        for (int i = _departureLandCount - 1; i >= 0; i--)
        {
            if (!IsTouchingLand(_departureLands[i]))
            {
                _departureLands[i] = _departureLands[--_departureLandCount];
                _departureLands[_departureLandCount] = null;
            }
        }
    }

    private bool IsDepartureLand(Collider land)
    {
        for (int i = 0; i < _departureLandCount; i++)
        {
            if (_departureLands[i] == land)
            {
                return true;
            }
        }
        return false;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleContact(collision);
    }

    public float GetDepartureHeading()
    {
        int count = CollectLand();
        Collider land = GetNearestLand(count);
        if (!land)
        {
            return transform.eulerAngles.y;
        }

        Vector3 direction = transform.position - land.bounds.center;
        if (Physics.ComputePenetration(_body, _body.transform.position,
            _body.transform.rotation, land, land.transform.position,
            land.transform.rotation, out Vector3 separation, out float distance))
        {
            separation.y = 0f;
            if (separation.sqrMagnitude > DirectionEpsilon)
            {
                direction = separation;
            }
        }

        direction.y = 0f;
        return direction.sqrMagnitude > DirectionEpsilon
            ? Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y
            : transform.eulerAngles.y;
    }

    public bool TryFindLandingPosition(PlayerController player, out Vector3 position)
    {
        position = default;
        if (!_body || !player || !player.TryGetComponent(out CapsuleCollider capsule) || capsule.direction != 1)
        {
            return false;
        }

        int count = CollectLand();
        for (int i = 0; i < count; i++)
        {
            Collider land = _lands[i];
            if (!IsTouchingLand(land) || IsDepartureLand(land))
            {
                continue;
            }

            Vector3 direction = GetLandPoint(land, _body.bounds.center) - _body.bounds.center;
            direction.y = 0f;
            if (direction.sqrMagnitude < DirectionEpsilon)
            {
                direction = land.bounds.center - _body.bounds.center;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < DirectionEpsilon)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            for (int j = 0; j < LandingDirectionCount; j++)
            {
                Vector3 candidateDirection = Quaternion.Euler(0f, j * (360f / LandingDirectionCount), 0f) * direction;
                if (TryGetLandingPosition(land, capsule, candidateDirection, out position))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void HandleContact(Collision collision)
    {
        if (!_boat)
        {
            return;
        }

        PlayerController owner = _boat.Owner;
        if (!owner && collision.rigidbody
            && collision.rigidbody.TryGetComponent(out PlayerController candidate)
            && _boat.TrySetOfflineOwner(candidate))
        {
            owner = candidate;
        }
        if (!owner || !owner.CanProcessBoatInput || !owner.isActiveAndEnabled || owner.IsDowned)
        {
            return;
        }

        if (!_boat.IsOccupied)
        {
            if (collision.rigidbody
                && collision.rigidbody.gameObject == owner.gameObject
                && TryGetDockZone(out DockZone dock)
                && dock.ContainsPosition(owner.transform.position))
            {
                _boat.RequestBoard(owner);
            }

            return;
        }

        int dockLayer = LayerMask.NameToLayer("Dock");

        if (dockLayer >= 0
            && collision.collider.gameObject.layer == dockLayer
            && TryGetDockZone(out DockZone dockZone))
        {
            _boat.RequestDisembark(owner);
        }
    }

    private int CollectLand()
    {
        int dockLayer = LayerMask.NameToLayer("Dock");

        if (!_body || dockLayer < 0)
        {
            return 0;
        }

        Bounds bounds = _body.bounds;

        int count = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents + Vector3.one * _contactTolerance,
            _lands,
            Quaternion.identity,
            1 << dockLayer,
            QueryTriggerInteraction.Ignore);

        return count == _lands.Length ? 0 : count;
    }

    private Collider GetNearestLand(int count)
    {
        Collider nearest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider land = _lands[i];
            if (!IsTouchingLand(land))
            {
                continue;
            }

            float distance = (GetLandPoint(land, _body.bounds.center) - _body.bounds.center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = land;
            }
        }

        return nearest;
    }

    private bool IsTouchingLand(Collider land)
    {
        if (!land || land == _body || !IsLand(land))
        {
            return false;
        }

        Vector3 landPoint = GetLandPoint(land, _body.bounds.center);
        Vector3 boatPoint = _body.ClosestPoint(landPoint);
        return (landPoint - boatPoint).sqrMagnitude <= _contactTolerance * _contactTolerance;
    }

    private static Vector3 GetLandPoint(Collider land, Vector3 point)
    {
        if (land is MeshCollider mesh && !mesh.convex)
        {
            return land.bounds.ClosestPoint(point);
        }

        return land.ClosestPoint(point);
    }

    private bool TryGetLandingPosition(Collider land, CapsuleCollider capsule, Vector3 direction, out Vector3 position)
    {
        position = default;
        Vector3 scale = capsule.transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
        Bounds bounds = _body.bounds;
        float support = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
        Vector3 origin = bounds.center + direction * (support + radius + _landingClearance);
        origin.y = bounds.max.y + _landingProbeHeight;

        if (!land.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, _landingProbeHeight * 2f + height)
            || Vector3.Angle(hit.normal, Vector3.up) > _maxLandingSlope)
        {
            return false;
        }

        Vector3 center = hit.point + Vector3.up * (height * 0.5f + _landingClearance);
        float halfSegment = height * 0.5f - radius;
        int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * halfSegment,
            center - Vector3.up * halfSegment, radius, _obstacles,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        if (count == _obstacles.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Collider obstacle = _obstacles[i];
            if (obstacle && !obstacle.transform.IsChildOf(capsule.transform))
            {
                return false;
            }
        }

        Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        position = center - rotation * Vector3.Scale(capsule.center, scale);
        return (position - transform.position).sqrMagnitude <= _maxLandingDistance * _maxLandingDistance;
    }

    private bool IsLand(Collider collider)
    {
        if (!collider || collider.isTrigger)
        {
            return false;
        }

        Rigidbody body = collider.attachedRigidbody;

        if (body
            && (body.TryGetComponent<PlayerController>(out _)
                || body.TryGetComponent<BoatController>(out _)))
        {
            return false;
        }

        int dockLayer = LayerMask.NameToLayer("Dock");

        return dockLayer >= 0
            && collider.gameObject.layer == dockLayer;
    }

    public bool TryFindDockLandingPosition(
    PlayerController player,
    out Vector3 position)
    {
        position = default;

        if (!_body || !player
            || !player.TryGetComponent(out CapsuleCollider capsule)
            || capsule.direction != 1)
        {
            return false;
        }

        if (!TryGetDockZone(out DockZone dock))
        {
            return false;
        }

        Collider[] surfaces = dock.LandingSurfaces;

        if (surfaces == null)
        {
            return false;
        }

        for (int i = 0; i < surfaces.Length; i++)
        {
            Collider surface = surfaces[i];

            if (!surface || !surface.enabled
                || !surface.gameObject.activeInHierarchy
                || !IsLand(surface))
            {
                continue;
            }

            for (int j = 0; j < LandingDirectionCount; j++)
            {
                float angle = j * (360f / LandingDirectionCount);

                Vector3 direction =
                    Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                if (TryGetLandingPosition(
                    surface, capsule, direction, out position))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetDockZone(out DockZone dock)
    {
        dock = null;

        if (!_body)
        {
            return false;
        }

        Bounds bounds = _body.bounds;

        int count = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            _dockCandidates,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        if (count == _dockCandidates.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Collider candidate = _dockCandidates[i];

            if (!candidate || !candidate.isTrigger || !candidate.TryGetComponent(out DockZone zone) || !zone.isActiveAndEnabled)
            {
                continue;
            }

            if (zone.ContainsPosition(bounds.center))
            {
                dock = zone;
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (!_boat)
        {
            _boat = GetComponent<BoatController>();
        }

        _body = GetComponent<BoxCollider>();
    }
}
