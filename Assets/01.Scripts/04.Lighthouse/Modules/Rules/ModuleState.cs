namespace Lighthouse.Modules.Rules
{
    // Where a module is in its life. Every state counts toward the recipe's existence cap; only disassembly
    // (and losing it with a wreck or a summon) removes the record (2026-10-17, 2026-10-20 decisions). Drifting is added with JH05.
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
        OnBoat = 5
    }
}
