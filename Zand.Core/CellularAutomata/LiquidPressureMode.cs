namespace Zand.Core.CellularAutomata;

// Whether or not the CA grid runs the more resource intensive, but in theory more realistic
// liquid pressure simulation. See LiquidPressure for more details.
public enum LiquidPressureMode
{
    Disabled,
    Enabled
}
