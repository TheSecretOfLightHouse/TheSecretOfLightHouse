namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    // 역할 판정 구현은 시험 환경과 본편에서 구분하며, 사람 식별자는 서버의 ActorId를 사용한다.
    public interface ISeaSummonAuthority
    {
        bool CanSummon(uint actorId);
    }
}
