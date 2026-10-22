namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    // 실제 역할 배정이 생기면 이 계약의 구현만 교체한다. 식별자는 서버의 플레이어 netId다.
    public interface ISeaSummonAuthority
    {
        bool CanSummon(uint playerNetId);
    }
}
