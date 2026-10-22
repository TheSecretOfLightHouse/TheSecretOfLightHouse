namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    public sealed class TrialSeaSummonAuthority : ISeaSummonAuthority
    {
        private readonly uint _playerNetId;

        public TrialSeaSummonAuthority(uint playerNetId)
        {
            _playerNetId = playerNetId;
        }

        public bool CanSummon(uint playerNetId)
        {
            return _playerNetId != 0 && playerNetId == _playerNetId;
        }
    }
}
