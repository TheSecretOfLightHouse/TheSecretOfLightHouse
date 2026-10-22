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
        Craft = 12,
        // Lighthouse match flow: start the ritual once fully repaired. 14 and 15 were the crystal relight, dropped on
        // 2026-10-22 when the final battle started carrying the repair over; do not reuse them.
        StartRitual = 13,
        // Carrying a finished module (2026-10-20): take one out of the workbench into your hands, or put it back.
        // Installing it is InstallModule on the lighthouse or on your own boat.
        TakeModule = 16,
        ReturnModule = 17,
        // Picking up a module left on a shore after a wreck (2026-10-22). Separate from PickUp, which is the resource drop.
        PickUpModule = 18
    }
}
