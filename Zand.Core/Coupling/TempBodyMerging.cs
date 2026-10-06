namespace Zand.Core.Coupling;

// To support several of the coupling types, temporary sand bodies that are near a rigid body
// are printed into the rigid body system as temporary static rigid bodies.
//
// This axis defines how the temporary bodies for sand cells near a rigid body are grouped.
// Fewer, larger bodies should mean fewer seams between neighbouring shapes for the physics engine to trip over,
// and might reduce the physics engine's overall processing time.
public enum TempBodyMerging
{
    // One body per sand cell.
    NoMerging,

    // One body per horizontal group of adjacent sand cells in a row.
    HorizontalMerging,

    // Horizontal runs, with runs of the same width in consecutive rows merged into a single rectangle.
    RectangleMerging
}
