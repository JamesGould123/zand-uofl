namespace Zand.Core.Tests.Ballistics;

using System.Numerics;
using Xunit;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

public class BallisticParticleSystemTests
{
    private const int WorldSize = 20;

    // ----- Detection and ejection gating -----
    [Fact]
    public void Advance_FirstTick_NeverEjectsRegardlessOfVelocity()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -100));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
        Assert.Empty(system.AirborneParticles);
    }

    [Fact]
    public void Advance_SecondTickWithVelocityAboveThreshold_EjectsImpactedCell()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -100));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Empty, grid.GetCell(10, 15).Type);
        Assert.Single(system.AirborneParticles);
    }

    [Fact]
    public void Advance_VelocityBelowEjectionThreshold_DoesNotEject()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -3));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
        Assert.Empty(system.AirborneParticles);
    }

    [Fact]
    public void Advance_StaticBody_NeverEjects()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddStaticBox(10.5f, 5.5f, 0.4f, 0.4f);
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_StaticCaCell_IsNeverEjected()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Static });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -100));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Static, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_ModeNone_DoesNothingAtAll()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -100));
        physics.SetLinearVelocityCalls.Clear();
        var system = CreateSystem(BallisticParticleMode.None);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
        Assert.Empty(physics.SetLinearVelocityCalls);
    }

    // ----- Ejection resistance -----
    [Fact]
    public void Eject_ImpulseModeCenterOfMass_AdjustsBodyVelocityOpposingImpact()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f, density: 1f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        physics.SetLinearVelocityCalls.Clear();
        var system = CreateSystem(BallisticParticleMode.PointSwarm, impulseTargetMode: EjectionImpulseTargetMode.CenterOfMass);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        // mass = 1 * 0.4 * 0.4 * 4 = 0.64; cellMass = 1600 (Sand); impulse = -1600 * (0,-10) = (0, 16000)
        // resulting velocity = (0,-10) + (0,16000)/0.64 = (0, 24990)
        var call = Assert.Single(physics.SetLinearVelocityCalls, c => c.Handle.Equals(handle));
        AssertVectorApprox(0f, 24990f, call.Velocity);
    }

    [Fact]
    public void Eject_ImpulseModeContactPoint_AppliesImpulseAtContactPointInsteadOfVelocity()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f, density: 1f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        physics.SetLinearVelocityCalls.Clear();
        var system = CreateSystem(BallisticParticleMode.PointSwarm, impulseTargetMode: EjectionImpulseTargetMode.ContactPoint);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        var impulse = Assert.Single(physics.AppliedImpulses, c => c.Handle.Equals(handle));
        AssertVectorApprox(0f, 16000f, impulse.Impulse);
        AssertVectorApprox(10.5f, 4.5f, impulse.Point);
        Assert.Empty(physics.SetLinearVelocityCalls);
    }

    [Fact]
    public void Eject_ImpulseModeWithNonPositiveBodyMass_SkipsResistance()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f, density: 0f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        physics.SetLinearVelocityCalls.Clear();
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        Assert.Equal(CellType.Empty, grid.GetCell(10, 15).Type);
        Assert.Empty(physics.SetLinearVelocityCalls);
        Assert.Empty(physics.AppliedImpulses);
    }

    [Fact]
    public void Eject_VelocityDampingMode_ScalesBodyVelocityByDampingFraction()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        physics.SetLinearVelocityCalls.Clear();
        var system = CreateSystem(BallisticParticleMode.PointSwarm, resistanceMode: EjectionResistanceMode.VelocityDamping);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        // default velocityDampingFraction = 0.01 -> (0,-10) * 0.99 = (0,-9.9)
        var call = Assert.Single(physics.SetLinearVelocityCalls, c => c.Handle.Equals(handle));
        AssertVectorApprox(0f, -9.9f, call.Velocity);
    }

    // ----- PointSwarm lifecycle -----
    [Fact]
    public void Advance_PointSwarmMode_EjectedCellBecomesAirborneParticle()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);

        var particle = Assert.Single(system.AirborneParticles);
        Assert.Equal(CellType.Sand, particle.CellType);
        AssertVectorApprox(10.5f, 4.5f, particle.Position);
        AssertVectorApprox(0f, -10f, particle.Velocity);
    }

    [Fact]
    public void Advance_AirborneParticle_FallsUnderGravityEachTick()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);
        system.Advance(grid, physics, 0.1f);

        var particle = Assert.Single(system.AirborneParticles);
        AssertVectorApprox(0f, -11f, particle.Velocity, tolerance: 1e-3f);
        AssertVectorApprox(10.5f, 3.4f, particle.Position, tolerance: 1e-3f);
    }

    [Fact]
    public void Advance_AirborneParticle_BouncesOffFilledCaCellAboveSettleThreshold()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        var system = CreateSystem(BallisticParticleMode.PointSwarm);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });

        system.Advance(grid, physics, 0.1f);

        var particle = Assert.Single(system.AirborneParticles);
        AssertVectorApprox(0f, 3.3f, particle.Velocity, tolerance: 1e-2f);
        AssertVectorApprox(10.5f, 4.5f, particle.Position, tolerance: 1e-3f);
    }

    [Fact]
    public void Advance_AirborneParticle_ReattachesWhenSlowEnoughOverValidGround()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -0.3f));
        var system = CreateSystem(BallisticParticleMode.PointSwarm, ejectionVelocityThreshold: 0.1f, gravityY: 0f);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 2f);

        Assert.Empty(system.AirborneParticles);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_AirborneParticle_OnCollisionMode_ReattachesRegardlessOfSpeed()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        var system = CreateSystem(BallisticParticleMode.PointSwarm, reattachmentMode: ReattachmentMode.OnCollision);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });

        system.Advance(grid, physics, 0.1f);

        Assert.Empty(system.AirborneParticles);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_AirborneParticle_CollidesWithRigidBody_BouncesOffBodySurface()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(0, -10));
        physics.AddStaticBox(10.5f, 3.4f, 1f, 1f);
        var system = CreateSystem(BallisticParticleMode.PointSwarm);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);

        system.Advance(grid, physics, 0.1f);

        var particle = Assert.Single(system.AirborneParticles);
        AssertVectorApprox(0f, 3.3f, particle.Velocity, tolerance: 1e-2f);
        AssertVectorApprox(10.5f, 4.5f, particle.Position, tolerance: 1e-3f);
    }

    [Fact]
    public void Advance_TwoParticlesProposingSameLandingCell_OnlyOneCommits()
    {
        var grid = CreateGrid(30, WorldSize);
        grid.SetCell(9, 14, new Cell { Type = CellType.Sand });
        grid.SetCell(11, 14, new Cell { Type = CellType.Sand });
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });
        for (int x = 8; x <= 12; x++)
        {
            grid.SetCell(x, 19, new Cell { Type = CellType.Static });
        }

        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 6.5f, 1f, 1f);
        physics.SetLinearVelocity(handle, new Vector2(0, -0.3f));
        var system = CreateSystem(BallisticParticleMode.PointSwarm, ejectionVelocityThreshold: 0.1f, gravityY: 0f);
        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 17.5f);

        Assert.Single(system.AirborneParticles);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    // ----- Landing cell search (via TempRigidBody's reattachment path) -----
    [Fact]
    public void TryFindLandingCell_PrefersDirectlyBelowOverDiagonalWhenBothValid()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);
        grid.SetCell(10, 17, new Cell { Type = CellType.Static });
        grid.SetCell(11, 17, new Cell { Type = CellType.Static });

        system.Advance(grid, physics, 0.1f);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 16).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(11, 16).Type);
    }

    [Fact]
    public void TryFindLandingCell_RejectsCellWhoseSupportIsLessDenseThanTheParticle()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);
        grid.SetCell(10, 17, new Cell { Type = CellType.Water });

        system.Advance(grid, physics, 0.1f);

        Assert.DoesNotContain(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Empty, grid.GetCell(10, 16).Type);
    }

    [Fact]
    public void TryFindLandingCell_NarrowMode_FailsWithNoWiderFallback()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody, landingSearchMode: LandingSearchMode.Narrow);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);
        grid.SetCell(14, 16, new Cell { Type = CellType.Static });

        system.Advance(grid, physics, 0.1f);

        Assert.DoesNotContain(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Empty, grid.GetCell(14, 15).Type);
    }

    [Fact]
    public void TryFindLandingCell_WideMode_FindsCellNarrowModeWouldMiss()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody, landingSearchMode: LandingSearchMode.Wide);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);
        grid.SetCell(13, 16, new Cell { Type = CellType.Static });

        system.Advance(grid, physics, 0.1f);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Sand, grid.GetCell(13, 15).Type);
    }

    // ----- TempRigidBody lifecycle -----
    [Fact]
    public void Advance_TempRigidBodyMode_EjectedCellSpawnsDynamicBodyWithImpactVelocity()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);

        var tempHandle = EjectSandAsTempBody(grid, physics, system);

        Assert.Empty(system.AirborneParticles);
        Assert.Equal(CellType.Empty, grid.GetCell(10, 15).Type);
        var tempBody = physics.GetBodies().Single(b => b.Handle.Equals(tempHandle));
        AssertVectorApprox(10.5f, 4.5f, tempBody.Position);
        AssertVectorApprox(0f, -10f, physics.GetLinearVelocity(tempHandle));
    }

    [Fact]
    public void Advance_TempRigidBody_ReattachesAndRemovesBodyWhenSlowEnough()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);

        system.Advance(grid, physics, 0.1f);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_TempRigidBody_ReattachesOnSuddenSpeedDropEvenIfStillFast()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        grid.SetCell(10, 16, new Cell { Type = CellType.Static });

        // LastSpeed was recorded as 10 at ejection; dropping to 2 (a drop of 8 > the default
        // ejection threshold of 6) counts as a hard impact even though 2 is still well above
        // the normal settle threshold of 0.5.
        physics.SetLinearVelocity(tempHandle, new Vector2(0, -2));
        system.Advance(grid, physics, 0.1f);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
    }

    [Fact]
    public void Advance_TempRigidBody_StaysAirborneWhenNoValidLandingCellExists()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        var system = CreateSystem(BallisticParticleMode.TempRigidBody);
        var tempHandle = EjectSandAsTempBody(grid, physics, system);
        physics.SetLinearVelocity(tempHandle, Vector2.Zero);

        system.Advance(grid, physics, 0.1f);

        Assert.DoesNotContain(tempHandle, physics.RemovedBodies);
        Assert.Contains(physics.GetBodies(), b => b.Handle.Equals(tempHandle));
    }

    // ----- Deflection -----
    [Fact]
    public void Eject_FaceNormalDeflection_AddsVelocityAlongSurfaceNormalComparedToDisabled()
    {
        var disabledVelocity = EjectAlongBoxFace(EjectionDeflectionMode.Disabled);
        var faceNormalVelocity = EjectAlongBoxFace(EjectionDeflectionMode.FaceNormal);

        // Impact velocity is (10,0); the box's nearest face in that direction has normal (1,0),
        // so FaceNormal deflection adds normal * |impactVelocity| * deflectionFactor(0.5) = (5,0).
        AssertVectorApprox(10f, 0f, disabledVelocity);
        AssertVectorApprox(15f, 0f, faceNormalVelocity);
    }

    // ----- Particle-particle separation -----
    [Fact]
    public void Advance_CollisionEnabled_OverlappingParticlesPushApartToExactlyMinSeparation()
    {
        var (a, b) = SetUpConvergingParticles(ParticleCollisionMode.Enabled, flightTime: 0.9f);

        // Regardless of the exact pre-separation gap, SeparatePair's correction always leaves
        // the pair exactly minSeparation (0.5, at cellsPerMeter=1) apart.
        Assert.Equal(0.5f, Vector2.Distance(a, b), precision: 3);
    }

    [Fact]
    public void Advance_CollisionDisabled_OverlappingParticlesAreUnaffected()
    {
        var (a, b) = SetUpConvergingParticles(ParticleCollisionMode.Disabled, flightTime: 0.9f);

        Assert.Equal(0.2f, Vector2.Distance(a, b), precision: 3);
    }

    [Fact]
    public void Advance_CollisionEnabled_ParticlesBeyondMinSeparation_AreUnaffected()
    {
        var (a, b) = SetUpConvergingParticles(ParticleCollisionMode.Enabled, flightTime: 0.5f);

        Assert.Equal(1.0f, Vector2.Distance(a, b), precision: 3);
    }

    // ----- Parallel resolution consistency -----
    [Fact]
    public void Advance_ParallelResolutionMatchesSequential_ForNonConflictingParticles()
    {
        var sequential = RunFallingRow(BallisticResolutionParallelism.Sequential);
        var parallel = RunFallingRow(BallisticResolutionParallelism.Parallel);

        Assert.Equal(sequential.Count, parallel.Count);
        for (int i = 0; i < sequential.Count; i++)
        {
            AssertVectorApprox(sequential[i].Position.X, sequential[i].Position.Y, parallel[i].Position);
            AssertVectorApprox(sequential[i].Velocity.X, sequential[i].Velocity.Y, parallel[i].Velocity);
        }
    }

    private static Vector2 EjectAlongBoxFace(EjectionDeflectionMode deflectionMode)
    {
        var grid = CreateGrid();
        grid.SetCell(14, 9, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 10.5f, 3f, 0.3f);
        physics.SetLinearVelocity(handle, new Vector2(10, 0));
        var system = CreateSystem(BallisticParticleMode.PointSwarm, deflectionMode: deflectionMode);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);

        return Assert.Single(system.AirborneParticles).Velocity;
    }

    private static (Vector2 A, Vector2 B) SetUpConvergingParticles(ParticleCollisionMode collisionMode, float flightTime)
    {
        var grid = CreateGrid();
        grid.SetCell(9, 15, new Cell { Type = CellType.Sand });
        grid.SetCell(11, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var bodyA = physics.AddDynamicBox(8f, 4.5f, 0.3f, 0.3f);
        physics.SetLinearVelocity(bodyA, new Vector2(1, 0));
        var bodyB = physics.AddDynamicBox(13f, 4.5f, 0.3f, 0.3f);
        physics.SetLinearVelocity(bodyB, new Vector2(-1, 0));
        var system = CreateSystem(
            BallisticParticleMode.PointSwarm, particleCollisionMode: collisionMode, ejectionVelocityThreshold: 0.1f, gravityY: 0f);

        system.Advance(grid, physics, 0.1f);

        // Same tick: both cells eject, then fly toward each other far enough to end up closer
        // than minSeparation (0.5) apart.
        system.Advance(grid, physics, flightTime);

        // Isolate the separation step alone, with no further movement of its own this tick.
        system.Advance(grid, physics, 0f);

        Assert.Equal(2, system.AirborneParticles.Count);
        return (system.AirborneParticles[0].Position, system.AirborneParticles[1].Position);
    }

    private static List<AirborneParticle> RunFallingRow(BallisticResolutionParallelism resolutionParallelism)
    {
        var grid = CreateGrid(30, WorldSize);
        for (int x = 5; x <= 14; x++)
        {
            grid.SetCell(x, 15, new Cell { Type = CellType.Sand });
        }

        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10f, 7.5f, 5f, 0.3f);
        physics.SetLinearVelocity(handle, new Vector2(0, -1));
        var system = CreateSystem(
            BallisticParticleMode.PointSwarm, resolutionParallelism: resolutionParallelism, ejectionVelocityThreshold: 0.1f);

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0f);
        system.Advance(grid, physics, 0.1f);

        return system.AirborneParticles.ToList();
    }

    private static BodyHandle EjectSandAsTempBody(CaGrid grid, FakeRigidBodySimulation physics, BallisticParticleSystem system)
    {
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var impactHandle = physics.AddDynamicBox(10.5f, 5.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(impactHandle, new Vector2(0, -10));

        system.Advance(grid, physics, 0.1f);
        system.Advance(grid, physics, 0.1f);

        return physics.GetBodies().First(b => !b.Handle.Equals(impactHandle)).Handle;
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize) =>
        new(width, height, new PerCellAlgorithm(1));

    private static BallisticParticleSystem CreateSystem(
        BallisticParticleMode mode,
        EjectionResistanceMode resistanceMode = EjectionResistanceMode.Impulse,
        ParticleCollisionMode particleCollisionMode = ParticleCollisionMode.Disabled,
        LandingSearchMode landingSearchMode = LandingSearchMode.Narrow,
        EjectionImpulseTargetMode impulseTargetMode = EjectionImpulseTargetMode.CenterOfMass,
        EjectionDeflectionMode deflectionMode = EjectionDeflectionMode.Disabled,
        ReattachmentMode reattachmentMode = ReattachmentMode.VelocitySettling,
        BallisticResolutionParallelism resolutionParallelism = BallisticResolutionParallelism.Sequential,
        float ejectionVelocityThreshold = 6f,
        float settleSpeedThreshold = 0.5f,
        float restitution = 0.3f,
        float friction = 0.4f,
        float gravityY = -10f) =>
        new(
            cellsPerMeter: 1f,
            worldHeightCells: WorldSize,
            gravityY: gravityY,
            mode: mode,
            resistanceMode: resistanceMode,
            particleCollisionMode: particleCollisionMode,
            landingSearchMode: landingSearchMode,
            impulseTargetMode: impulseTargetMode,
            deflectionMode: deflectionMode,
            reattachmentMode: reattachmentMode,
            resolutionParallelism: resolutionParallelism,
            ejectionVelocityThreshold: ejectionVelocityThreshold,
            settleSpeedThreshold: settleSpeedThreshold,
            restitution: restitution,
            friction: friction,
            scatterSpeed: 0f);

    private static void AssertVectorApprox(float expectedX, float expectedY, Vector2 actual, float tolerance = 1e-2f)
    {
        Assert.True(MathF.Abs(actual.X - expectedX) < tolerance, $"X: expected {expectedX}, got {actual.X}");
        Assert.True(MathF.Abs(actual.Y - expectedY) < tolerance, $"Y: expected {expectedY}, got {actual.Y}");
    }
}
