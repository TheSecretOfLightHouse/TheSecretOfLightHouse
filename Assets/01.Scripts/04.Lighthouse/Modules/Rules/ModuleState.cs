namespace Lighthouse.Modules.Rules
{
    // Where a module is in its life. Every state counts toward the recipe's existence cap; only disassembly
    // removes the record (2026-10-17 decision). Drifting/carried are added with JH05.
    public enum ModuleState : byte
    {
        None = 0,
        Crafting = 1,
        Stored = 2,
        Installed = 3
    }
}
