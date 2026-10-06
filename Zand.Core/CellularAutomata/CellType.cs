namespace Zand.Core.CellularAutomata;

// Changing the underlying storage to a byte improves speed of the simulation slightly
// Citation: Cagigas-Muñiz, D., Diaz-del-Rio, F., Sevillano-Ramos, J. L., & Guisado-Lizar, J.-L. (2022).
// Efficient simulation execution of cellular automata on GPU. Simulation Modelling Practice and Theory,
// 118, 102519. Sec. 4.2 (Cell size).
public enum CellType : byte
{
    Empty,
    Sand,
    Water,
    Static
}
