namespace Lighthouse.Mafia.SeaSummoning.Rules
{
    public sealed class TrialSeaSummonAuthority : ISeaSummonAuthority
    {
        private readonly uint _actorId;

        public TrialSeaSummonAuthority(uint actorId)
        {
            _actorId = actorId;
        }

        public bool CanSummon(uint actorId)
        {
            return _actorId != 0 && actorId == _actorId;
        }
    }
}
