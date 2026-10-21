namespace Lighthouse.Tower.Rules
{
    // Server-only. The voting side counts the votes; the lighthouse only needs the number when the ritual ends,
    // so no vote or role list passes through here.
    public interface IRitualVoteJudge
    {
        int CountCitizenVotesOnMafia();
    }
}
