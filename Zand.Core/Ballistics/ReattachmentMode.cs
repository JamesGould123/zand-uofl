namespace Zand.Core.Ballistics;

// When a ballistic particle reattaches to the CA grid
// - VelocitySettling: Settles when velocity is low enough. Allows more cells to fly,
//      as they can fly "through" CA cells when beginning
// - OnCollision: reattach when it touches a solid CA cell, regardless of speed.
//      Produces a less visually realistic effect, but preserves mass.
public enum ReattachmentMode
{
    VelocitySettling,
    OnCollision
}
