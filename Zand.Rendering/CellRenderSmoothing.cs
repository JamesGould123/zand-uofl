namespace Zand.Rendering;

// Purely cosmetic - changes how Empty cells are rendered, never effects the simulation itself.
// Implemented to ameliorate artifacts that show up on chunk boundaries when water is rapidly moving laterally.
// See CellRenderer.Draw
public enum CellRenderSmoothing
{
    Disabled,
    Smoothing,
    NeighborFill
}
