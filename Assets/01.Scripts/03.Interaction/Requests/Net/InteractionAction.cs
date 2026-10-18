namespace Lighthouse.Interaction.Requests.Net
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
        InspectGhostShip = 9,
        RepairBoat = 10,
        Rescue = 11,
        // Finishes a workbench recipe whose ingredients are all in (2026-10-17: completion is a separate press).
        Craft = 12
    }
}
