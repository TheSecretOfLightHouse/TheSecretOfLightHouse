using Lighthouse.World.Query.Rules;
using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(NavigatorRouteRequest))]
public sealed class NavigatorRouteView : MonoBehaviour
{
    private const int MaxRenderPoints = 2048;
    private const float MinPointSpacing = 0.1f;

    [SerializeField]
    private Material _lineMaterial;

    [SerializeField, Min(0.1f)]
    private float _lineWidth = 1.5f;

    [SerializeField, Min(0.1f)]
    private float _destinationRadius = 2f;

    [SerializeField, Min(0.05f)]
    private float _destinationWidth = 0.35f;

    [SerializeField, Min(0.1f)]
    private float _heightOffset = 0.5f;

    [SerializeField, Min(MinPointSpacing)]
    private float _pointSpacing = 5f;

    [SerializeField, Min(0.02f)]
    private float _refreshInterval = 0.1f;

    [SerializeField]
    private Color _lineColor = new Color(0.2f, 1f, 0.85f, 1f);

    private readonly List<Vector3> _renderPoints =
        new List<Vector3>();

    private PlayerController _player;
    private PlayerPassiveController _passives;
    private NavigatorRouteRequest _request;
    private PlayerController _target;
    private LineRenderer _line;
    private LineRenderer _destinationRing;
    private float _nextRefreshTime;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _passives = GetComponent<PlayerPassiveController>();
        _request = GetComponent<NavigatorRouteRequest>();
    }

    private void OnEnable()
    {
        _request.RouteReceived += HandleRouteReceived;
    }

    private void Update()
    {
        if (!_target)
        {
            if (_renderPoints.Count > 0)
            {
                ClearRoute();
            }

            return;
        }

        if (!NetworkClient.active || !_player.isLocalPlayer
            || !_player.isActiveAndEnabled || _player.IsDowned
            || !_target.isActiveAndEnabled || !_target.IsDowned
            || !_request || !_request.isActiveAndEnabled
            || !_passives || !_passives.isActiveAndEnabled
            || !_passives.IsJobAbilityEnabled)
        {
            ClearRoute();
            return;
        }

        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + _refreshInterval;
        RefreshLine();
    }

    private void OnDisable()
    {
        if (_request)
        {
            _request.RouteReceived -= HandleRouteReceived;
        }

        ClearRoute();
    }

    private void OnDestroy()
    {
        if (_line)
        {
            Destroy(_line.gameObject);
        }

        if (_destinationRing)
        {
            Destroy(_destinationRing.gameObject);
        }
    }

    public void ClearRoute()
    {
        _target = null;
        _renderPoints.Clear();

        if (_line)
        {
            _line.positionCount = 0;
            _line.enabled = false;
        }

        if (_destinationRing)
        {
            _destinationRing.enabled = false;
        }
    }

    private void HandleRouteReceived(
        uint targetNetId,
        Vector2[] route)
    {
        ClearRoute();

        if (!NetworkClient.active || !_player.isLocalPlayer
            || route == null || route.Length < 2
            || route.Length >= MaxRenderPoints)
        {
            return;
        }

        if (!NetworkClient.spawned.TryGetValue(
                targetNetId, out NetworkIdentity identity)
            || !identity
            || !identity.TryGetComponent(out PlayerController target)
            || !target.isActiveAndEnabled || !target.IsDowned)
        {
            return;
        }

        if (!_lineMaterial)
        {
            Debug.LogError(
                $"[{nameof(NavigatorRouteView)}] Route material is missing.");
            return;
        }

        if (!BuildRenderPoints(route))
        {
            return;
        }

        EnsureLine();

        _target = target;
        _nextRefreshTime = 0f;
        RefreshLine();
    }

    private bool BuildRenderPoints(Vector2[] route)
    {
        float totalLength = 0f;

        for (int i = 0; i < route.Length; i++)
        {
            if (!IsFinite(route[i].x) || !IsFinite(route[i].y))
            {
                return false;
            }

            if (i > 0)
            {
                totalLength += Vector2.Distance(route[i - 1], route[i]);
            }
        }

        if (!IsFinite(totalLength)
            || !IsFinite(_pointSpacing))
        {
            return false;
        }

        float spacing = Mathf.Max(
            MinPointSpacing,
            Mathf.Max(
                _pointSpacing,
                totalLength / (MaxRenderPoints - route.Length)));

        _renderPoints.Add(
            new Vector3(route[0].x, 0f, route[0].y));

        for (int i = 1; i < route.Length; i++)
        {
            Vector2 from = route[i - 1];
            Vector2 to = route[i];

            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(Vector2.Distance(from, to) / spacing));

            for (int step = 1; step <= steps; step++)
            {
                Vector2 point = Vector2.Lerp(
                    from, to, (float)step / steps);

                _renderPoints.Add(
                    new Vector3(point.x, 0f, point.y));
            }
        }

        return true;
    }

    private void EnsureLine()
    {
        if (_line)
        {
            return;
        }

        // This object exists only on the receiving client's local scene.
        var lineObject = new GameObject("NavigatorRouteLine");
        lineObject.transform.SetParent(transform, false);

        _line = lineObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.loop = false;
        _line.sharedMaterial = _lineMaterial;
        _line.widthMultiplier = _lineWidth;
        _line.startColor = _lineColor;
        _line.endColor = _lineColor;
        _line.shadowCastingMode = ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.textureMode = LineTextureMode.Tile;
        _line.numCornerVertices = 6;
        _line.numCapVertices = 6;
        _line.enabled = false;

        var ringObject = new GameObject("NavigatorDestinationRing");
        ringObject.transform.SetParent(transform, false);
        _destinationRing = ringObject.AddComponent<LineRenderer>();
        _destinationRing.useWorldSpace = true;
        _destinationRing.loop = true;
        _destinationRing.sharedMaterial = _lineMaterial;
        _destinationRing.widthMultiplier = _destinationWidth;
        _destinationRing.startColor = _lineColor;
        _destinationRing.endColor = _lineColor;
        _destinationRing.textureMode = LineTextureMode.Tile;
        _destinationRing.numCornerVertices = 4;
        _destinationRing.shadowCastingMode = ShadowCastingMode.Off;
        _destinationRing.receiveShadows = false;
        _destinationRing.positionCount = 48;
        _destinationRing.enabled = false;
    }

    private void RefreshLine()
    {
        if (!_line || _renderPoints.Count < 2)
        {
            return;
        }

        if (!WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
        {
            _line.enabled = false;
            _destinationRing.enabled = false;
            return;
        }

        _line.positionCount = _renderPoints.Count;

        for (int i = 0; i < _renderPoints.Count; i++)
        {
            Vector3 position = _renderPoints[i];
            float height = world.SampleWaterHeight(position);

            if (!IsFinite(height))
            {
                _line.enabled = false;
                _destinationRing.enabled = false;
                return;
            }

            position.y = height + _heightOffset;
            _line.SetPosition(i, position);
        }

        _line.enabled = true;

        // Keep the marker on the accepted route endpoint, even if the target falls below water.
        Vector3 center = _renderPoints[_renderPoints.Count - 1];
        for (int i = 0; i < _destinationRing.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / _destinationRing.positionCount;
            Vector3 position = center + new Vector3(
                Mathf.Cos(angle) * _destinationRadius, 0f,
                Mathf.Sin(angle) * _destinationRadius);
            float height = world.SampleWaterHeight(position);
            if (!IsFinite(height))
            {
                _destinationRing.enabled = false;
                return;
            }

            position.y = height + _heightOffset;
            _destinationRing.SetPosition(i, position);
        }

        _destinationRing.enabled = true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
