using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;

namespace Lighthouse.Network.Session.View
{
    // Wording follows win-vote-decisions §8: "left" and "lost connection" stay distinct, and a silent drop stays neutral.
    internal static class SessionEndText
    {
        // False when nothing needs explaining, e.g. this player chose to leave.
        public static bool TryDescribe(SessionEndedMessage end, uint localActorId, out string title, out string message)
        {
            title = "판 종료";

            switch (end.Reason)
            {
                case SessionEndReason.ParticipantLeft:
                    if (end.ActorId != 0 && end.ActorId == localActorId)
                    {
                        break;
                    }

                    message = $"플레이어 {end.ActorId}님이 나갔습니다.\n판이 종료되었습니다.";
                    return true;
                case SessionEndReason.ParticipantDisconnected:
                    message = $"플레이어 {end.ActorId}님의 연결이 끊겼습니다.\n판이 종료되었습니다.";
                    return true;
                case SessionEndReason.LoadTimeout:
                    message = "로딩 시간이 초과되어\n판이 종료되었습니다.";
                    return true;
                case SessionEndReason.JoinRejected:
                    title = "참가 불가";
                    message = "진행 중인 판에는 참가할 수 없습니다.";
                    return true;
                case SessionEndReason.ConnectionLost:
                    message = "연결이 끊겨 판이 종료되었습니다.";
                    return true;
                case SessionEndReason.LobbyConnectionLost:
                    title = "로비 종료";
                    message = "연결이 끊겨 로비에서 나왔습니다.";
                    return true;
            }

            message = null;
            return false;
        }
    }
}
