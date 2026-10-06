namespace Zand.Core.Ballistics;

using System.Numerics;
using Zand.Core.CellularAutomata;

// Cells in the CA are unable to accurately represent impacts/momentum,
// so when BallisticParticleMode is PointSwarm
// we convert CA particles to ballistic particles temporarily -
// then convert them back into the CA system when appropriate
// (on collision with a filled CA cell).
public struct AirborneParticle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public CellType CellType;
}
