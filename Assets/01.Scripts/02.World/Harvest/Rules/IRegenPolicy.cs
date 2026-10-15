namespace Lighthouse.World.Harvest.Rules
{
    public interface IRegenPolicy
    {
        int GetRegenAmount(int maxAmount, float dailyRegenRate, int current, int days);
    }
}
