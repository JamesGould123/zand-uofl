namespace Zand.Core.CellularAutomata.Algorithms;

// In the per cell algorithms, cells are visited either starting from the top or starting from the bottom.
// (Oscillating between left to right and right to left)
// This config axis allows for the cells to be visited in several different ways. This is useful for preventing
// certain visual artifacts, such as "walls" of water that can form on chunk borders.
// Options other than BottomUp should only be used with SameTickMoveGuard enabled.
// Oscillating goes between BottomUp and TopDown repetitively, where Randomized switches randomly between the two.
public enum RowSweepOrder
{
    BottomUp,
    TopDown,
    Oscillating,
    Randomized
}
