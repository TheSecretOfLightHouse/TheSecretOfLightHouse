namespace Lighthouse.Modules.Rules
{
    // Where a module is in its life. Every state but OnGround counts toward the recipe's existence cap; only disassembly
    // (and losing it with a wreck, a summon or rotting on a shore) removes the record (2026-10-17, 2026-10-20, 2026-10-22 decisions).
    public enum ModuleState : byte
    {
        None = 0,
        Crafting = 1,
        Stored = 2,
        // In the lighthouse.
        Installed = 3,
        // In a player's hands, taken out of the workbench.
        Carried = 4,
        // On a player's own boat.
        OnBoat = 5,
        // Left on a shore by a player who was not rescued (2026-10-22). Outside the cap, so the recipe can be made
        // again while it lies there; picking it up brings it back in even past the cap.
        OnGround = 6
    }
}
