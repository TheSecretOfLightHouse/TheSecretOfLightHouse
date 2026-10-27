using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerAbilityUsage))]
public sealed class NavigatorRouteRequest : NetworkBehaviour
{
    private const int MaxRoutePoints = 512;

    private readonly List<Vector2> _route = new List<Vector2>();

    private PlayerController _player;
    private PlayerAbilityUsage _usage;
    private PlayerAbilityRunner _runner;
    private NavigatorRouteAbility _ability;
    private PlayerPassiveController _passives;

    private uint _nextRequestId;
    private uint _pendingRequestId;
    private uint _lastServerRequestId;
    private uint _lastServerTargetNetId;

    private NavigatorRouteResult _lastServerResult;
    private Vector2[] _lastServerRoute = Array.Empty<Vector2>();

    public bool IsAwaitingResult { get; private set; }

    public event Action<NavigatorRouteResult> Responded;
    public event Action<uint, Vector2[]> RouteReceived;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _usage = GetComponent<PlayerAbilityUsage>();
        _runner = GetComponent<PlayerAbilityRunner>();
        _ability = GetComponent<NavigatorRouteAbility>();
        _passives = GetComponent<PlayerPassiveController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _lastServerRequestId = 0;
        _lastServerTargetNetId = 0;
        _lastServerRoute = Array.Empty<Vector2>();
    }

    public override void OnStopClient()
    {
        IsAwaitingResult = false;
        base.OnStopClient();
    }

    public bool TryRequest(PlayerController target)
    {
        if (!isActiveAndEnabled || !isClient || !isOwned
            || !NetworkClient.ready || IsAwaitingResult
            || !target || target.netId == 0
            || !_ability || !_runner
            || _runner.CurrentAbility != _ability
            || _nextRequestId == uint.MaxValue)
        {
            return false;
        }

        _pendingRequestId = ++_nextRequestId;

        // A host can receive its response before the Command returns.
        IsAwaitingResult = true;
        CmdRequestRoute(_pendingRequestId, target.netId);
        return true;
    }

    [Command]
    private void CmdRequestRoute(
        uint requestId,
        uint targetNetId,
        NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender != connectionToClient
            || sender.identity != netIdentity)
        {
            return;
        }

        NavigatorRouteResult result;
        Vector2[] route;

        if (requestId == 0 || requestId < _lastServerRequestId
            || (requestId == _lastServerRequestId
                && targetNetId != _lastServerTargetNetId))
        {
            result = NavigatorRouteResult.RequestRejected;
            route = Array.Empty<Vector2>();
        }
        else if (requestId == _lastServerRequestId)
        {
            result = _lastServerResult;
            route = _lastServerRoute;
        }
        else
        {
            _lastServerRequestId = requestId;
            _lastServerTargetNetId = targetNetId;

            result = TryCreateRoute(targetNetId, out route);

            _lastServerResult = result;
            _lastServerRoute = route;
        }

        TargetRespond(
            sender, requestId, targetNetId, result, route);
    }

    [Server]
    private NavigatorRouteResult TryCreateRoute(
        uint targetNetId,
        out Vector2[] route)
    {
        route = Array.Empty<Vector2>();

        LighthouseNetworkManager manager =
            LighthouseNetworkManager.Instance;

        if (manager && manager.IsSessionFlowEnabled
            && manager.Phase != MatchPhase.InProgress)
        {
            return NavigatorRouteResult.MatchUnavailable;
        }

        if (!isActiveAndEnabled
            || !_player || !_player.isActiveAndEnabled
            || _player.IsDowned
            || !_ability || !_ability.isActiveAndEnabled
            || !_runner || _runner.JobAbility != _ability
            || !_usage || !_usage.isActiveAndEnabled
            || !_passives || !_passives.isActiveAndEnabled
            || !_passives.IsJobAbilityEnabled)
        {
            return NavigatorRouteResult.InvalidCaster;
        }

        if (!WorldServices.TryGet<IGameClock>(out IGameClock clock)
            || clock.DayIndex < 1)
        {
            return NavigatorRouteResult.ClockUnavailable;
        }

        if (!NetworkServer.spawned.TryGetValue(
                targetNetId, out NetworkIdentity identity)
            || !identity || identity == netIdentity
            || !identity.TryGetComponent(out PlayerController target)
            || !target.isActiveAndEnabled || !target.IsDowned
            || !target.TryGetComponent(
                out PlayerWreckRecovery recovery)
            || !recovery.isActiveAndEnabled
            || !recovery.RescueTarget
            || !recovery.RescueTarget.isActiveAndEnabled)
        {
            return NavigatorRouteResult.InvalidTarget;
        }

        BoatController boat = _player.OwnedBoat;

        if (!boat || !boat.isActiveAndEnabled || !boat.isServer
            || boat.IsWrecked || boat.Owner != _player)
        {
            return NavigatorRouteResult.BoatUnavailable;
        }

        if (!_usage.CanUse(PlayerAbilityKind.NavigatorRoute))
        {
            return NavigatorRouteResult.AlreadyUsed;
        }

        if (!WorldServices.TryGet(
                out NavigatorRoutePlanner planner)
            || !planner || !planner.isActiveAndEnabled)
        {
            return NavigatorRouteResult.RouteUnavailable;
        }

        Vector3 start = boat.transform.position;
        Vector3 destination = recovery.RescueTarget.Position;
        Vector2[] calculatedRoute = Array.Empty<Vector2>();

        bool succeeded = _usage.TryExecute(
            PlayerAbilityKind.NavigatorRoute,
            () =>
            {
                if (!planner.TryFindPath(start, destination, _route))
                {
                    return false;
                }

                RemoveCollinearPoints(_route);

                if (_route.Count < 2
                    || _route.Count > MaxRoutePoints)
                {
                    return false;
                }

                calculatedRoute = _route.ToArray();
                return true;
            });

        if (!succeeded)
        {
            return NavigatorRouteResult.RouteUnavailable;
        }

        route = calculatedRoute;
        return NavigatorRouteResult.Success;
    }

    [TargetRpc]
    private void TargetRespond(
        NetworkConnectionToClient target,
        uint requestId,
        uint targetNetId,
        NavigatorRouteResult result,
        Vector2[] route)
    {
        if (!IsAwaitingResult || requestId != _pendingRequestId)
        {
            return;
        }

        IsAwaitingResult = false;

        if (result == NavigatorRouteResult.Success)
        {
            RouteReceived?.Invoke(targetNetId, route);
        }

        Responded?.Invoke(result);
    }

    private static void RemoveCollinearPoints(List<Vector2> points)
    {
        int writeIndex = 0;
        int originalCount = points.Count;

        for (int readIndex = 0;
            readIndex < originalCount;
            readIndex++)
        {
            Vector2 next = points[readIndex];

            while (writeIndex >= 2)
            {
                Vector2 previous = points[writeIndex - 2];
                Vector2 current = points[writeIndex - 1];

                Vector2 first = current - previous;
                Vector2 second = next - current;

                float cross = first.x * second.y
                    - first.y * second.x;

                if (cross != 0f || Vector2.Dot(first, second) < 0f)
                {
                    break;
                }

                writeIndex--;
            }

            points[writeIndex] = next;
            writeIndex++;
        }

        if (writeIndex < originalCount)
        {
            points.RemoveRange(
                writeIndex, originalCount - writeIndex);
        }
    }
}