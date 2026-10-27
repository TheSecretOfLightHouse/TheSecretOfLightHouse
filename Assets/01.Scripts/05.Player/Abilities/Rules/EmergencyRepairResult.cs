using UnityEngine;

public enum EmergencyRepairResult : byte
{
    Success,
    AlreadyUsed,
    InvalidCaster,
    InvalidTarget,
    AlreadyFull,
    MatchUnavailable,
    ClockUnavailable,
    RequestRejected
}
