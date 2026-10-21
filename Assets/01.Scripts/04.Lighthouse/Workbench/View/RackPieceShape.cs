namespace Lighthouse.Workbench.View
{
    // Stand-in shapes for resources on the workbench rack until real models exist.
    public enum RackPieceShape : byte
    {
        // A box: planks, stone blocks, glass panes.
        Box,
        // A cylinder lying front to back: iron bars, rope coils.
        Rod,
        // A sphere: crystals.
        Ball,
    }
}
