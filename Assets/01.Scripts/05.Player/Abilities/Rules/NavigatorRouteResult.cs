using UnityEngine;

public enum NavigatorRouteResult : byte
{
    Success,
    AlreadyUsed,
    InvalidCaster,
    InvalidTarget,
    BoatUnavailable,
    RouteUnavailable,
    MatchUnavailable,
    ClockUnavailable,
    RequestRejected
}