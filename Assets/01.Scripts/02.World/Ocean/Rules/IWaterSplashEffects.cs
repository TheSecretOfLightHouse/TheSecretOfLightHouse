namespace Lighthouse.World.Ocean.Rules
{
    public interface IWaterSplashEffects
    {
        bool Play(in WaterSplashRequest request);
    }
}
