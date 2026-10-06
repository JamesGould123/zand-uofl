namespace Zand.Core.CellularAutomata;

// When enabled, prevents cells in the PerCell algorithms from moving twice in a single tick.
// Prevents issues with RowSweepOrders other than BottomUp
// (if gases, or other particles that travel upwards by default, are added later,
// SameTickMoveGuard should likely always be enabled. This is because the BottomUp/no tick guard approach
// to updating cells was built assuming that particles will only travel downwards)
public enum SameTickMoveGuard
{
    Disabled,
    Enabled
}
