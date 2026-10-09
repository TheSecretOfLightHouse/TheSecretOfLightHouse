namespace Lighthouse.Map.Net.Contracts
{
    public enum InteractionAction : byte
    {
        None = 0,
        Harvest = 1,
        Deposit = 2,
        Withdraw = 3,
        SelectRecipe = 4,
        InstallModule = 5,
        RepairModule = 6,
        PickUp = 7,
        UseSpecialIsland = 8,
        InspectGhostShip = 9
    }
}
