namespace Zand.App.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImGuiNET;
using Zand.App.Config;
using Zand.App.Scenarios;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Rendering;

public class MenuScreen
{
    // This file saves the last config used to disk, that way upon reopening the app,
    // the same config is loaded when possible.
    // It is saved to the same folder as the metrics for the last run.
    private const string ConfigFolder = "zand-uofl";
    private const string ConfigFileName = "last-config.json";

    private const int MinRepeatCount = 1;
    private const int MaxRepeatCount = 50;
    private const uint MaxFilePrefixLength = 64;

    private const float TogglePillRounding = 4f;

    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] BenchmarkAxisNames = Enum.GetNames<BenchmarkAxis>();

    private static readonly HashSet<BenchmarkAxis> HeavyBenchmarkAxes =
    [
        BenchmarkAxis.WorldSize, BenchmarkAxis.ChunkSize, BenchmarkAxis.ActiveRectangle, BenchmarkAxis.Parallelism,
        BenchmarkAxis.RasterizationBodyParallelism, BenchmarkAxis.RasterizationCellParallelism,
        BenchmarkAxis.BallisticResolutionParallelism, BenchmarkAxis.RenderParallelism
    ];

    private readonly Dictionary<WorldSizeOption, bool> _worldSizeOptions;
    private readonly Dictionary<CaAlgorithmType, bool> _caAlgorithms;
    private readonly Dictionary<RigidBodySimulationType, bool> _rbSimulations;
    private readonly Dictionary<CouplingMode, bool> _couplingModes;
    private readonly Dictionary<LiquidPhysicsMode, bool> _liquidPhysicsModes;
    private readonly Dictionary<GravityMode, bool> _gravityModes;
    private readonly Dictionary<TempBodyLifetime, bool> _tempBodyLifetimes;
    private readonly Dictionary<TempBodyMerging, bool> _tempBodyMergings;
    private readonly Dictionary<ChunkSize, bool> _chunkSizes;
    private readonly Dictionary<ActiveRectangle, bool> _activeRectangleModes;
    private readonly Dictionary<ParallelismMode, bool> _parallelismModes;
    private readonly Dictionary<LiquidPressureMode, bool> _liquidPressureModes;
    private readonly Dictionary<SameTickMoveGuard, bool> _sameTickMoveGuards;
    private readonly Dictionary<RowSweepOrder, bool> _rowSweepOrders;
    private readonly Dictionary<CellRenderSmoothing, bool> _cellRenderSmoothings;
    private readonly Dictionary<BallisticParticleMode, bool> _ballisticParticleModes;
    private readonly Dictionary<EjectionResistanceMode, bool> _ejectionResistanceModes;
    private readonly Dictionary<ParticleCollisionMode, bool> _particleCollisionModes;
    private readonly Dictionary<LandingSearchMode, bool> _landingSearchModes;
    private readonly Dictionary<EjectionImpulseTargetMode, bool> _ejectionImpulseTargetModes;
    private readonly Dictionary<EjectionDeflectionMode, bool> _ejectionDeflectionModes;
    private readonly Dictionary<ReattachmentMode, bool> _reattachmentModes;
    private readonly Dictionary<RasterizationBodyParallelism, bool> _rasterizationBodyParallelisms;
    private readonly Dictionary<RasterizationCellParallelism, bool> _rasterizationCellParallelisms;
    private readonly Dictionary<BallisticResolutionParallelism, bool> _ballisticResolutionParallelisms;
    private readonly Dictionary<RenderParallelism, bool> _renderParallelisms;
    private readonly Dictionary<string, bool> _scenarios;
    private readonly Dictionary<string, bool> _elements;
    private readonly List<string> _coreScenarioNames;
    private readonly List<string> _heavyScenarioNames;
    private readonly List<IAxisSection> _axisSections;
    private readonly Dictionary<BenchmarkAxis, IAxisSection> _axisSectionsByBenchmarkAxis;
    private ScenarioConfigMode _mode = ScenarioConfigMode.ByPreconfiguredScenarios;
    private BenchmarkAxis _reasonableTestAxis = BenchmarkAxis.CaAlgorithm;
    private bool _showActiveChunkDebug;
    private bool _realTimePlayback;
    private bool _showActiveRectangleDebug;
    private int _repeatCount = 1;
    private string _filePrefix = string.Empty;
    private string? _pasteError;

    public MenuScreen()
    {
        _worldSizeOptions = CreateSection<WorldSizeOption>(v => v == WorldSizeOption.Small512);
        _caAlgorithms = CreateSection<CaAlgorithmType>(v => v == CaAlgorithmType.Margolus);
        _rbSimulations = CreateSection<RigidBodySimulationType>(v => v == RigidBodySimulationType.Box2D);
        _couplingModes = CreateSection<CouplingMode>(v =>
            v == CouplingMode.WeakOneWayCaToRb || v == CouplingMode.WeakOneWayRbToCa);
        _liquidPhysicsModes = CreateSection<LiquidPhysicsMode>(v => v == LiquidPhysicsMode.BuoyancyAndDamping);
        _gravityModes = CreateSection<GravityMode>(v => v == GravityMode.Normal);
        _tempBodyLifetimes = CreateSection<TempBodyLifetime>(v => v == TempBodyLifetime.Ephemeral);
        _tempBodyMergings = CreateSection<TempBodyMerging>(v => v == TempBodyMerging.NoMerging);
        _chunkSizes = CreateSection<ChunkSize>(v => v == ChunkSize.Disabled);
        _activeRectangleModes = CreateSection<ActiveRectangle>(v => v == ActiveRectangle.Disabled);
        _parallelismModes = CreateSection<ParallelismMode>(v => v == ParallelismMode.Sequential);
        _liquidPressureModes = CreateSection<LiquidPressureMode>(v => v == LiquidPressureMode.Disabled);
        _sameTickMoveGuards = CreateSection<SameTickMoveGuard>(v => v == SameTickMoveGuard.Disabled);
        _rowSweepOrders = CreateSection<RowSweepOrder>(v => v == RowSweepOrder.BottomUp);
        _cellRenderSmoothings = CreateSection<CellRenderSmoothing>(v => v == CellRenderSmoothing.Disabled);
        _ballisticParticleModes = CreateSection<BallisticParticleMode>(v => v == BallisticParticleMode.None);
        _ejectionResistanceModes = CreateSection<EjectionResistanceMode>(v => v == EjectionResistanceMode.Impulse);
        _particleCollisionModes = CreateSection<ParticleCollisionMode>(v => v == ParticleCollisionMode.Disabled);
        _landingSearchModes = CreateSection<LandingSearchMode>(v => v == LandingSearchMode.Narrow);
        _ejectionImpulseTargetModes = CreateSection<EjectionImpulseTargetMode>(v => v == EjectionImpulseTargetMode.CenterOfMass);
        _ejectionDeflectionModes = CreateSection<EjectionDeflectionMode>(v => v == EjectionDeflectionMode.Disabled);
        _reattachmentModes = CreateSection<ReattachmentMode>(v => v == ReattachmentMode.VelocitySettling);
        _rasterizationBodyParallelisms = CreateSection<RasterizationBodyParallelism>(v => v == RasterizationBodyParallelism.Sequential);
        _rasterizationCellParallelisms = CreateSection<RasterizationCellParallelism>(v => v == RasterizationCellParallelism.Sequential);
        _ballisticResolutionParallelisms = CreateSection<BallisticResolutionParallelism>(v => v == BallisticResolutionParallelism.Sequential);
        _renderParallelisms = CreateSection<RenderParallelism>(v => v == RenderParallelism.Sequential);

        _scenarios = ScenarioRunner.AvailableScenarios
            .Select(s => s.Name)
            .ToDictionary(name => name, _ => true);

        _elements = ScenarioRunner.AvailableElements
            .ToDictionary(name => name, _ => false);

        _coreScenarioNames = ScenarioRunner.AvailableCoreScenarios.ToList();
        _heavyScenarioNames = ScenarioRunner.AvailableHeavyScenarios.ToList();

        var axisSectionsByBenchmarkAxis = new (BenchmarkAxis Axis, IAxisSection Section)[]
        {
            (BenchmarkAxis.WorldSize, new AxisSection<WorldSizeOption>(_worldSizeOptions)),
            (BenchmarkAxis.CaAlgorithm, new AxisSection<CaAlgorithmType>(_caAlgorithms)),
            (BenchmarkAxis.RbSimulation, new AxisSection<RigidBodySimulationType>(_rbSimulations)),
            (BenchmarkAxis.CouplingMode, new AxisSection<CouplingMode>(_couplingModes)),
            (BenchmarkAxis.LiquidPhysics, new AxisSection<LiquidPhysicsMode>(_liquidPhysicsModes)),
            (BenchmarkAxis.Gravity, new AxisSection<GravityMode>(_gravityModes)),
            (BenchmarkAxis.TempBodyLifetime, new AxisSection<TempBodyLifetime>(_tempBodyLifetimes)),
            (BenchmarkAxis.TempBodyMerging, new AxisSection<TempBodyMerging>(_tempBodyMergings)),
            (BenchmarkAxis.ChunkSize, new AxisSection<ChunkSize>(_chunkSizes)),
            (BenchmarkAxis.ActiveRectangle, new AxisSection<ActiveRectangle>(_activeRectangleModes)),
            (BenchmarkAxis.Parallelism, new AxisSection<ParallelismMode>(_parallelismModes)),
            (BenchmarkAxis.LiquidPressure, new AxisSection<LiquidPressureMode>(_liquidPressureModes)),
            (BenchmarkAxis.SameTickMoveGuard, new AxisSection<SameTickMoveGuard>(_sameTickMoveGuards)),
            (BenchmarkAxis.RowSweepOrder, new AxisSection<RowSweepOrder>(_rowSweepOrders)),
            (BenchmarkAxis.CellRenderSmoothing, new AxisSection<CellRenderSmoothing>(_cellRenderSmoothings)),
            (BenchmarkAxis.BallisticParticles, new AxisSection<BallisticParticleMode>(_ballisticParticleModes)),
            (BenchmarkAxis.EjectionResistance, new AxisSection<EjectionResistanceMode>(_ejectionResistanceModes)),
            (BenchmarkAxis.ParticleCollision, new AxisSection<ParticleCollisionMode>(_particleCollisionModes)),
            (BenchmarkAxis.LandingSearch, new AxisSection<LandingSearchMode>(_landingSearchModes)),
            (BenchmarkAxis.EjectionImpulseTarget, new AxisSection<EjectionImpulseTargetMode>(_ejectionImpulseTargetModes)),
            (BenchmarkAxis.EjectionDeflection, new AxisSection<EjectionDeflectionMode>(_ejectionDeflectionModes)),
            (BenchmarkAxis.Reattachment, new AxisSection<ReattachmentMode>(_reattachmentModes)),
            (BenchmarkAxis.RasterizationBodyParallelism, new AxisSection<RasterizationBodyParallelism>(_rasterizationBodyParallelisms)),
            (BenchmarkAxis.RasterizationCellParallelism, new AxisSection<RasterizationCellParallelism>(_rasterizationCellParallelisms)),
            (BenchmarkAxis.BallisticResolutionParallelism, new AxisSection<BallisticResolutionParallelism>(_ballisticResolutionParallelisms)),
            (BenchmarkAxis.RenderParallelism, new AxisSection<RenderParallelism>(_renderParallelisms))
        };

        _axisSections = axisSectionsByBenchmarkAxis.Select(pair => pair.Section).ToList();
        _axisSectionsByBenchmarkAxis = axisSectionsByBenchmarkAxis.ToDictionary(pair => pair.Axis, pair => pair.Section);

        var savedSelection = TryLoadSelection();
        if (savedSelection != null)
        {
            ApplySelection(savedSelection);
        }
    }

    private interface IAxisSection
    {
        void SaveBackup();

        void RestoreBackup();

        void SelectAll();

        int CheckedCount();
    }

    private static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ConfigFolder, ConfigFileName);

    public void Draw(Action<MenuSelection> onPlay)
    {
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(
            SimConstants.MenuWidthCells * SimConstants.CellSize,
            SimConstants.MenuHeightCells * SimConstants.CellSize));
        ImGui.Begin(
            "Zand Scenario Runner",
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Repeat Count", ref _repeatCount))
        {
            _repeatCount = Math.Clamp(_repeatCount, MinRepeatCount, MaxRepeatCount);
        }

        ImGui.SameLine();
        ImGui.TextDisabled("Runs the whole selected queue this many times over");

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("File Prefix", ref _filePrefix, MaxFilePrefixLength);
        ImGui.SameLine();
        ImGui.TextDisabled("Prepended to the saved metrics folder name");
        ImGui.Separator();

        ImGui.BeginTable("MenuColumns", 4, ImGuiTableFlags.BordersInnerV);

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        DrawConfigSection("World Size", BenchmarkAxis.WorldSize, _worldSizeOptions, c => c.WorldSize);
        DrawConfigSection("CA Algorithm", BenchmarkAxis.CaAlgorithm, _caAlgorithms, c => c.CaAlgorithm);
        DrawConfigSection("Chunk Size", BenchmarkAxis.ChunkSize, _chunkSizes, c => c.ChunkSize);
        ImGui.Checkbox("Show Active Chunk Debug", ref _showActiveChunkDebug);
        DrawConfigSection("Active Rectangle", BenchmarkAxis.ActiveRectangle, _activeRectangleModes, c => c.ActiveRectangle);
        ImGui.Checkbox("Show Active Rectangle Debug", ref _showActiveRectangleDebug);
        ImGui.Checkbox("Real-time Playback", ref _realTimePlayback);
        ImGui.TextDisabled("Caps at 60 FPS for watching - not for benchmarks");
        DrawConfigSection("Parallelism", BenchmarkAxis.Parallelism, _parallelismModes, c => c.Parallelism);
        DrawConfigSection("Rasterization Body Parallelism", BenchmarkAxis.RasterizationBodyParallelism, _rasterizationBodyParallelisms, c => c.RasterizationBodyParallelism);
        DrawConfigSection("Rasterization Cell Parallelism", BenchmarkAxis.RasterizationCellParallelism, _rasterizationCellParallelisms, c => c.RasterizationCellParallelism);
        DrawConfigSection("Render Parallelism", BenchmarkAxis.RenderParallelism, _renderParallelisms, c => c.RenderParallelism);

        ImGui.TableSetColumnIndex(1);
        DrawConfigSection("Rigid Body Sim", BenchmarkAxis.RbSimulation, _rbSimulations, c => c.RbSimulation);
        DrawConfigSection("Coupling Mode", BenchmarkAxis.CouplingMode, _couplingModes, c => c.Coupling);
        DrawConfigSection("Liquid Physics", BenchmarkAxis.LiquidPhysics, _liquidPhysicsModes, c => c.LiquidPhysics);
        DrawConfigSection("Cell Render Smoothing", BenchmarkAxis.CellRenderSmoothing, _cellRenderSmoothings, c => c.CellRenderSmoothing);
        DrawConfigSection("Gravity", BenchmarkAxis.Gravity, _gravityModes, c => c.Gravity);
        DrawConfigSection("Temp Body Lifetime", BenchmarkAxis.TempBodyLifetime, _tempBodyLifetimes, c => c.TempBodyLifetime);
        DrawConfigSection("Temp Body Merging", BenchmarkAxis.TempBodyMerging, _tempBodyMergings, c => c.TempBodyMerging);
        DrawConfigSection("Liquid Pressure", BenchmarkAxis.LiquidPressure, _liquidPressureModes, c => c.LiquidPressure);
        DrawConfigSection("Same-Tick Move Guard", BenchmarkAxis.SameTickMoveGuard, _sameTickMoveGuards, c => c.SameTickMoveGuard);
        DrawConfigSection("Row Sweep Order", BenchmarkAxis.RowSweepOrder, _rowSweepOrders, c => c.RowSweepOrder);

        ImGui.TableSetColumnIndex(2);
        DrawConfigSection("Ballistic Particles", BenchmarkAxis.BallisticParticles, _ballisticParticleModes, c => c.BallisticParticles);
        DrawConfigSection("Ballistic Resolution Parallelism", BenchmarkAxis.BallisticResolutionParallelism, _ballisticResolutionParallelisms, c => c.BallisticResolutionParallelism);
        DrawConfigSection("Ejection Resistance", BenchmarkAxis.EjectionResistance, _ejectionResistanceModes, c => c.EjectionResistance);
        DrawConfigSection("Particle Collision", BenchmarkAxis.ParticleCollision, _particleCollisionModes, c => c.ParticleCollision, asToggle: true);
        DrawConfigSection("Reattachment", BenchmarkAxis.Reattachment, _reattachmentModes, c => c.Reattachment);
        DrawConfigSection("Wide Landing Search", BenchmarkAxis.LandingSearch, _landingSearchModes, c => c.LandingSearch, asToggle: true);
        DrawConfigSection("Impulse At Contact Point", BenchmarkAxis.EjectionImpulseTarget, _ejectionImpulseTargetModes, c => c.EjectionImpulseTarget, asToggle: true);
        DrawConfigSection("Face-Normal Deflection", BenchmarkAxis.EjectionDeflection, _ejectionDeflectionModes, c => c.EjectionDeflection, asToggle: true);

        int reasonableCombinations = 0;
        int heavyCombinations = 0;

        ImGui.TableSetColumnIndex(3);
        DrawModeSwitch();
        ImGui.Separator();
        if (_mode == ScenarioConfigMode.ByPreconfiguredScenarios)
        {
            ImGui.Text("Scenarios");
            ImGui.PushID("Scenarios");
            foreach (var name in _scenarios.Keys.ToList())
            {
                bool value = _scenarios[name];
                if (ImGui.Checkbox(name, ref value))
                {
                    _scenarios[name] = value;
                }
            }

            ImGui.PopID();
        }
        else if (_mode == ScenarioConfigMode.ByElements)
        {
            ImGui.Text("Elements");
            ImGui.PushID("Elements");
            foreach (var name in _elements.Keys.ToList())
            {
                bool value = _elements[name];
                if (ImGui.Checkbox(name, ref value))
                {
                    _elements[name] = value;
                }
            }

            ImGui.PopID();
            ImGui.TextDisabled("Runs until you press '-' to end it");
        }
        else if (_mode == ScenarioConfigMode.CoreScenariosReasonable)
        {
            ImGui.Text("Core Scenarios");
            foreach (var name in _coreScenarioNames)
            {
                ImGui.BulletText(name);
            }

            ImGui.Separator();
            ImGui.Text("Test Axis");
            int testAxisIndex = (int)_reasonableTestAxis;
            if (ImGui.Combo("##TestAxis", ref testAxisIndex, BenchmarkAxisNames, BenchmarkAxisNames.Length))
            {
                var newTestAxis = (BenchmarkAxis)testAxisIndex;
                if (newTestAxis != _reasonableTestAxis)
                {
                    _reasonableTestAxis = newTestAxis;
                    _axisSectionsByBenchmarkAxis[_reasonableTestAxis].SelectAll();
                }
            }

            ImGui.Separator();
            var (combinations, totalFrames) = EstimateReasonableRun();
            reasonableCombinations = combinations;
            ImGui.TextWrapped(
                $"{Enum.GetName(_reasonableTestAxis)}: {_axisSectionsByBenchmarkAxis[_reasonableTestAxis].CheckedCount()} " +
                $"value(s) x 4 reasonable config(s) = {combinations} valid combination(s)");
            ImGui.TextWrapped(
                $"{combinations} combination(s) x {_coreScenarioNames.Count} core scenario(s) x " +
                $"{_repeatCount} repeat(s) = {totalFrames} frames total");
            ImGui.TextWrapped($"Estimated run time: {FormatDuration(totalFrames / BenchmarkEstimator.AssumedFramesPerSecond)} (assumes ~60 FPS)");
        }
        else
        {
            ImGui.Text("Heavy Scenario");
            foreach (var name in _heavyScenarioNames)
            {
                ImGui.BulletText(name);
            }

            ImGui.Separator();
            var (combinations, totalFrames) = EstimateHeavyRun();
            heavyCombinations = combinations;
            ImGui.TextWrapped(
                $"{combinations} valid World Size x Chunk Size x Active Rectangle x Parallelism x " +
                "Rasterization Body Parallelism x Rasterization Cell Parallelism x Ballistic Resolution " +
                "Parallelism x reasonable config combination(s)");
            ImGui.TextWrapped(
                $"{combinations} combination(s) x {ScenarioRunner.HeavyScenarioFrameBudget} frame(s) x " +
                $"{_repeatCount} repeat(s) = {totalFrames} frames total");
            ImGui.TextWrapped($"Estimated run time: {FormatDuration(totalFrames / BenchmarkEstimator.AssumedFramesPerSecond)} (assumes ~60 FPS)");
        }

        ImGui.EndTable();

        ImGui.Separator();
        if (AnyCategoryEmpty())
        {
            ImGui.TextDisabled("Select at least one option in every category.");
        }
        else if (!HasValidCouplingLiquidCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: Coupling Mode 'None' and 'WeakOneWayRbToCa' have no " +
                "mechanism to use Liquid Physics, so they only pair with Liquid Physics 'None'. " +
                "Select a different Coupling Mode or set Liquid Physics to 'None'.");
            ImGui.PopStyleColor();
        }
        else if (!HasValidParallelismCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: 'Parallel' and 'ParallelLargestFirst' have nothing " +
                "to spread across threads with Chunk Size 'Disabled', so they only pair with a real " +
                "chunk size. Select a different Parallelism mode or a non-Disabled Chunk Size.");
            ImGui.PopStyleColor();
        }
        else if (!HasValidBallisticCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: the ejection-tuning options only mean something " +
                "once Ballistic Particles is not 'None' (and Particle Collision only means " +
                "something under 'PointSwarm'), so those cases only pair with every tuning option " +
                "back at its default. Select a Ballistic Particles mode, or set the tuning options " +
                "back to their defaults.");
            ImGui.PopStyleColor();
        }
        else if (!HasValidBallisticResolutionParallelismCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: 'Parallel' Ballistic Resolution Parallelism only " +
                "parallelizes the 'PointSwarm' particle resolution loop, so it has nothing to spread " +
                "across threads with Ballistic Particles 'None' or 'TempRigidBody'. Select a different " +
                "Ballistic Resolution Parallelism or the 'PointSwarm' Ballistic Particles mode.");
            ImGui.PopStyleColor();
        }
        else if (!HasValidTempBodyLifetimeCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: Coupling Mode 'None' and 'WeakOneWayRbToCa' never " +
                "create temporary bodies, so Temp Body Lifetime has no effect there and they only " +
                "pair with 'Persistent'. Select 'Persistent' or a different Coupling Mode. " +
                "Liquid Physics 'ProbabilisticCollision' only pairs with 'Ephemeral', since " +
                "Persistent temp bodies never include liquid cells.");
            ImGui.PopStyleColor();
        }
        else if (!HasValidTempBodyMergingCombination())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: Coupling Mode 'None' and 'WeakOneWayRbToCa' never " +
                "create temporary bodies, so Temp Body Merging has no effect there and they only " +
                "pair with 'NoMerging'. Select 'NoMerging' or a different Coupling Mode.");
            ImGui.PopStyleColor();
        }
        else if (_mode == ScenarioConfigMode.CoreScenariosReasonable && reasonableCombinations == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: none of the checked Test Axis values pair validly " +
                "with any of the 4 reasonable configs. Check a different value for the test axis.");
            ImGui.PopStyleColor();
        }
        else if (_mode == ScenarioConfigMode.HeavyBenchmark && heavyCombinations == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(
                "No valid combination selected: none of the checked World Size / Chunk Size / Active " +
                "Rectangle / Parallelism / Rasterization Body Parallelism / Rasterization Cell " +
                "Parallelism / Ballistic Resolution Parallelism values pair validly with any of the 4 " +
                "reasonable configs (Parallel and ParallelLargestFirst require a non-Disabled Chunk " +
                "Size). Check a different value.");
            ImGui.PopStyleColor();
        }
        else if (ImGui.Button("Play"))
        {
            var selection = BuildSelection();
            SaveSelection(selection);
            onPlay(selection);
        }

        ImGui.SameLine();
        DrawCopyPasteControls();

        ImGui.End();
    }

    private static Dictionary<T, bool> CreateSection<T>(Func<T, bool> defaultChecked)
        where T : struct, Enum
    {
        return Enum.GetValues<T>().ToDictionary(v => v, defaultChecked);
    }

    private static void DrawSection<T>(string label, Dictionary<T, bool> values)
        where T : struct, Enum
    {
        ImGui.Text(label);
        ImGui.PushID(label);
        foreach (var key in values.Keys.ToList())
        {
            bool value = values[key];
            if (ImGui.Checkbox(key.ToString(), ref value))
            {
                values[key] = value;
            }
        }

        ImGui.PopID();
    }

    private static void DrawSingleSelectSection<T>(string label, Dictionary<T, bool> values)
        where T : struct, Enum
    {
        ImGui.Text(label);
        ImGui.PushID(label);
        foreach (var key in values.Keys.ToList())
        {
            if (ImGui.RadioButton(key.ToString(), values[key]))
            {
                SelectOnly(values, key);
            }
        }

        ImGui.PopID();
    }

    private static void DrawToggleSection<T>(string label, Dictionary<T, bool> values)
        where T : struct, Enum
    {
        var members = Enum.GetValues<T>();
        T off = members[0];
        T on = members[1];
        bool isOn = values[on];
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, TogglePillRounding);
        if (ImGui.Checkbox(label, ref isOn))
        {
            SelectOnly(values, isOn ? on : off);
        }

        ImGui.PopStyleVar();
    }

    private static void DrawReasonableAxisSection<T>(
        string label, Dictionary<T, bool> values, Func<SampleConfig, T> reasonableSelector, bool isFreeAxis)
        where T : struct, Enum
    {
        if (!isFreeAxis)
        {
            var union = ReasonableConfigs.Union(reasonableSelector);
            foreach (var key in values.Keys.ToList())
            {
                values[key] = union.Contains(key);
            }
        }

        ImGui.BeginDisabled(!isFreeAxis);
        DrawSection(label, values);
        ImGui.EndDisabled();
    }

    private static void SelectOnly<T>(Dictionary<T, bool> values, T selected)
        where T : notnull
    {
        foreach (var key in values.Keys.ToList())
        {
            values[key] = EqualityComparer<T>.Default.Equals(key, selected);
        }
    }

    private static void CollapseToSingleSelection<T>(Dictionary<T, bool> values)
        where T : notnull
    {
        var keys = values.Keys.ToList();
        int selectedIndex = keys.FindIndex(k => values[k]);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        SelectOnly(values, keys[selectedIndex]);
    }

    private static bool NoneChecked<T>(Dictionary<T, bool> values)
        where T : notnull
    {
        return values.Values.All(v => !v);
    }

    private static T[] Checked<T>(Dictionary<T, bool> values)
        where T : notnull
    {
        return values.Where(kv => kv.Value).Select(kv => kv.Key).ToArray();
    }

    private static void ApplyChecked<T>(Dictionary<T, bool> values, T[]? checkedKeys)
        where T : notnull
    {
        if (checkedKeys == null)
        {
            return;
        }

        var checkedSet = checkedKeys.ToHashSet();
        foreach (var key in values.Keys.ToList())
        {
            values[key] = checkedSet.Contains(key);
        }
    }

    private static MenuSelection? TryLoadSelection()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<MenuSelection>(File.ReadAllText(ConfigFilePath), ConfigJsonOptions);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // If the config contract has changed, it can fail to load. In this case we fall back to a default selection.
            return null;
        }
    }

    private static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s"
            : span.TotalMinutes >= 1
                ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
                : $"{span.Seconds}s";
    }

    private static void SaveSelection(MenuSelection selection)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ConfigFolder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ConfigFileName), JsonSerializer.Serialize(selection, ConfigJsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Graceful degradation, failing to save the config to disk shouldn't prevent running the simulation.
        }
    }

    private void DrawCopyPasteControls()
    {
        if (ImGui.Button("Copy Config"))
        {
            ImGui.SetClipboardText(JsonSerializer.Serialize(BuildSelection(), ConfigJsonOptions));
            _pasteError = null;
        }

        ImGui.SameLine();
        if (ImGui.Button("Paste Config"))
        {
            try
            {
                var selection = JsonSerializer.Deserialize<MenuSelection>(ImGui.GetClipboardText(), ConfigJsonOptions);
                if (selection == null)
                {
                    _pasteError = "Clipboard is empty.";
                }
                else
                {
                    ApplySelection(selection);
                    _pasteError = null;
                }
            }
            catch (JsonException)
            {
                _pasteError = "Clipboard doesn't contain a valid config.";
            }
        }

        if (_pasteError != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_pasteError);
        }
    }

    private void DrawModeSwitch()
    {
        ImGui.PushID("ScenarioConfigMode");
        bool byScenarios = _mode == ScenarioConfigMode.ByPreconfiguredScenarios;
        if (ImGui.RadioButton("By Preconfigured Scenarios", byScenarios))
        {
            SetMode(ScenarioConfigMode.ByPreconfiguredScenarios);
        }

        bool byElements = _mode == ScenarioConfigMode.ByElements;
        if (ImGui.RadioButton("By Elements", byElements))
        {
            SetMode(ScenarioConfigMode.ByElements);
        }

        bool coreScenariosReasonable = _mode == ScenarioConfigMode.CoreScenariosReasonable;
        if (ImGui.RadioButton("Core Scenarios With Reasonable Configs", coreScenariosReasonable))
        {
            SetMode(ScenarioConfigMode.CoreScenariosReasonable);
        }

        bool heavyBenchmark = _mode == ScenarioConfigMode.HeavyBenchmark;
        if (ImGui.RadioButton("Heavy Benchmark", heavyBenchmark))
        {
            SetMode(ScenarioConfigMode.HeavyBenchmark);
        }

        ImGui.PopID();
    }

    private void SetMode(ScenarioConfigMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        if (_mode is ScenarioConfigMode.CoreScenariosReasonable or ScenarioConfigMode.HeavyBenchmark)
        {
            foreach (var section in _axisSections)
            {
                section.RestoreBackup();
            }
        }

        _mode = mode;
        if (mode == ScenarioConfigMode.CoreScenariosReasonable)
        {
            foreach (var section in _axisSections)
            {
                section.SaveBackup();
            }

            _axisSectionsByBenchmarkAxis[_reasonableTestAxis].SelectAll();
        }
        else if (mode == ScenarioConfigMode.HeavyBenchmark)
        {
            foreach (var section in _axisSections)
            {
                section.SaveBackup();
            }

            foreach (var axis in HeavyBenchmarkAxes)
            {
                _axisSectionsByBenchmarkAxis[axis].SelectAll();
            }
        }
        else if (mode == ScenarioConfigMode.ByElements)
        {
            CollapseToSingleSelection(_worldSizeOptions);
            CollapseToSingleSelection(_caAlgorithms);
            CollapseToSingleSelection(_chunkSizes);
            CollapseToSingleSelection(_activeRectangleModes);
            CollapseToSingleSelection(_parallelismModes);
            CollapseToSingleSelection(_rbSimulations);
            CollapseToSingleSelection(_couplingModes);
            CollapseToSingleSelection(_liquidPhysicsModes);
            CollapseToSingleSelection(_gravityModes);
            CollapseToSingleSelection(_tempBodyLifetimes);
            CollapseToSingleSelection(_tempBodyMergings);
            CollapseToSingleSelection(_liquidPressureModes);
            CollapseToSingleSelection(_sameTickMoveGuards);
            CollapseToSingleSelection(_rowSweepOrders);
            CollapseToSingleSelection(_cellRenderSmoothings);
            CollapseToSingleSelection(_ballisticParticleModes);
            CollapseToSingleSelection(_ejectionResistanceModes);
            CollapseToSingleSelection(_particleCollisionModes);
            CollapseToSingleSelection(_landingSearchModes);
            CollapseToSingleSelection(_ejectionImpulseTargetModes);
            CollapseToSingleSelection(_ejectionDeflectionModes);
            CollapseToSingleSelection(_reattachmentModes);
            CollapseToSingleSelection(_rasterizationBodyParallelisms);
            CollapseToSingleSelection(_rasterizationCellParallelisms);
            CollapseToSingleSelection(_renderParallelisms);
        }
    }

    private void DrawConfigSection<T>(
        string label, BenchmarkAxis axis, Dictionary<T, bool> values, Func<SampleConfig, T> reasonableSelector, bool asToggle = false)
        where T : struct, Enum
    {
        if (_mode == ScenarioConfigMode.ByElements)
        {
            if (asToggle)
            {
                DrawToggleSection(label, values);
            }
            else
            {
                DrawSingleSelectSection(label, values);
            }
        }
        else if (_mode == ScenarioConfigMode.CoreScenariosReasonable)
        {
            DrawReasonableAxisSection(label, values, reasonableSelector, isFreeAxis: axis == _reasonableTestAxis);
        }
        else if (_mode == ScenarioConfigMode.HeavyBenchmark)
        {
            DrawReasonableAxisSection(label, values, reasonableSelector, isFreeAxis: HeavyBenchmarkAxes.Contains(axis));
        }
        else
        {
            DrawSection(label, values);
        }
    }

    private bool AnyCategoryEmpty()
    {
        bool activeListEmpty = _mode switch
        {
            ScenarioConfigMode.ByPreconfiguredScenarios => NoneChecked(_scenarios),
            ScenarioConfigMode.ByElements => NoneChecked(_elements),
            _ => false
        };

        return NoneChecked(_worldSizeOptions)
            || NoneChecked(_caAlgorithms)
            || NoneChecked(_chunkSizes)
            || NoneChecked(_activeRectangleModes)
            || NoneChecked(_parallelismModes)
            || NoneChecked(_rbSimulations)
            || NoneChecked(_couplingModes)
            || NoneChecked(_liquidPhysicsModes)
            || NoneChecked(_gravityModes)
            || NoneChecked(_tempBodyLifetimes)
            || NoneChecked(_tempBodyMergings)
            || NoneChecked(_liquidPressureModes)
            || NoneChecked(_sameTickMoveGuards)
            || NoneChecked(_rowSweepOrders)
            || NoneChecked(_cellRenderSmoothings)
            || NoneChecked(_ballisticParticleModes)
            || NoneChecked(_ejectionResistanceModes)
            || NoneChecked(_particleCollisionModes)
            || NoneChecked(_landingSearchModes)
            || NoneChecked(_ejectionImpulseTargetModes)
            || NoneChecked(_ejectionDeflectionModes)
            || NoneChecked(_reattachmentModes)
            || NoneChecked(_rasterizationBodyParallelisms)
            || NoneChecked(_rasterizationCellParallelisms)
            || NoneChecked(_ballisticResolutionParallelisms)
            || NoneChecked(_renderParallelisms)
            || activeListEmpty;
    }

    private bool HasValidCouplingLiquidCombination()
    {
        var couplingModes = Checked(_couplingModes);
        var liquidPhysicsModes = Checked(_liquidPhysicsModes);
        return couplingModes.Any(coupling =>
            liquidPhysicsModes.Any(liquid => ScenarioRunner.IsValidCombination(coupling, liquid)));
    }

    private bool HasValidTempBodyLifetimeCombination()
    {
        var couplingModes = Checked(_couplingModes);
        var tempBodyLifetimes = Checked(_tempBodyLifetimes);
        var liquidPhysicsModes = Checked(_liquidPhysicsModes);
        return couplingModes.Any(coupling =>
            tempBodyLifetimes.Any(lifetime => ScenarioRunner.IsValidTempBodyLifetime(coupling, lifetime)
                && liquidPhysicsModes.Any(liquid => ScenarioRunner.IsValidCombination(coupling, liquid)
                    && ScenarioRunner.IsValidLiquidTempBodyLifetime(liquid, lifetime))));
    }

    private bool HasValidTempBodyMergingCombination()
    {
        var couplingModes = Checked(_couplingModes);
        var tempBodyMergings = Checked(_tempBodyMergings);
        return couplingModes.Any(coupling =>
            tempBodyMergings.Any(merging => ScenarioRunner.IsValidTempBodyMerging(coupling, merging)));
    }

    private bool HasValidParallelismCombination()
    {
        var parallelismModes = Checked(_parallelismModes);
        var chunkSizes = Checked(_chunkSizes);
        return parallelismModes.Any(parallelism =>
            chunkSizes.Any(chunkSize => ScenarioRunner.IsValidParallelism(parallelism, chunkSize)));
    }

    private bool HasValidBallisticCombination()
    {
        var ballisticModes = Checked(_ballisticParticleModes);
        var resistanceModes = Checked(_ejectionResistanceModes);
        var particleCollisionModes = Checked(_particleCollisionModes);
        var landingSearchModes = Checked(_landingSearchModes);
        var impulseTargetModes = Checked(_ejectionImpulseTargetModes);
        var deflectionModes = Checked(_ejectionDeflectionModes);
        var reattachmentModes = Checked(_reattachmentModes);

        return (
            from mode in ballisticModes
            from resistance in resistanceModes
            from particleCollision in particleCollisionModes
            from landingSearch in landingSearchModes
            from impulseTarget in impulseTargetModes
            from deflection in deflectionModes
            from reattachment in reattachmentModes
            where ScenarioRunner.IsValidBallisticCombination(mode, resistance,
                particleCollision, landingSearch, impulseTarget, deflection, reattachment)
            select true)
        .Any();
    }

    private bool HasValidBallisticResolutionParallelismCombination()
    {
        var ballisticModes = Checked(_ballisticParticleModes);
        var resolutionParallelisms = Checked(_ballisticResolutionParallelisms);
        return ballisticModes.Any(mode =>
            resolutionParallelisms.Any(resolutionParallelism =>
                ScenarioRunner.IsValidBallisticResolutionParallelism(mode, resolutionParallelism)));
    }

    private string[] SelectedScenarioNames()
    {
        return _mode switch
        {
            ScenarioConfigMode.ByPreconfiguredScenarios => Checked(_scenarios),
            ScenarioConfigMode.CoreScenariosReasonable => _coreScenarioNames.ToArray(),
            _ => []
        };
    }

    private MenuSelection BuildSelection()
    {
        return new MenuSelection(
            Checked(_worldSizeOptions),
            Checked(_caAlgorithms),
            Checked(_rbSimulations),
            Checked(_couplingModes),
            Checked(_liquidPhysicsModes),
            Checked(_gravityModes),
            Checked(_tempBodyLifetimes),
            Checked(_chunkSizes),
            Checked(_activeRectangleModes),
            Checked(_parallelismModes),
            Checked(_liquidPressureModes),
            Checked(_sameTickMoveGuards),
            Checked(_rowSweepOrders),
            Checked(_cellRenderSmoothings),
            Checked(_ballisticParticleModes),
            Checked(_ejectionResistanceModes),
            Checked(_particleCollisionModes),
            Checked(_landingSearchModes),
            Checked(_ejectionImpulseTargetModes),
            Checked(_ejectionDeflectionModes),
            Checked(_reattachmentModes),
            Checked(_rasterizationBodyParallelisms),
            Checked(_rasterizationCellParallelisms),
            Checked(_ballisticResolutionParallelisms),
            Checked(_renderParallelisms),
            _mode,
            SelectedScenarioNames(),
            _mode == ScenarioConfigMode.ByElements ? Checked(_elements) : [],
            _showActiveChunkDebug,
            _showActiveRectangleDebug,
            _repeatCount,
            _reasonableTestAxis,
            _filePrefix,
            TempBodyMergings: Checked(_tempBodyMergings),
            RealTimePlayback: _realTimePlayback);
    }

    private void ApplySelection(MenuSelection selection)
    {
        ApplyChecked(_worldSizeOptions, selection.WorldSizeOptions);
        ApplyChecked(_caAlgorithms, selection.CaAlgorithms);
        ApplyChecked(_rbSimulations, selection.RbSimulations);
        ApplyChecked(_couplingModes, selection.CouplingModes);
        ApplyChecked(_liquidPhysicsModes, selection.LiquidPhysicsModes);
        ApplyChecked(_gravityModes, selection.GravityModes);
        ApplyChecked(_tempBodyLifetimes, selection.TempBodyLifetimes);
        ApplyChecked(_tempBodyMergings, selection.TempBodyMergingChoices);
        ApplyChecked(_chunkSizes, selection.ChunkSizes);
        ApplyChecked(_activeRectangleModes, selection.ActiveRectangleModes);
        ApplyChecked(_parallelismModes, selection.ParallelismModes);
        ApplyChecked(_liquidPressureModes, selection.LiquidPressureModes);
        ApplyChecked(_sameTickMoveGuards, selection.SameTickMoveGuards);
        ApplyChecked(_rowSweepOrders, selection.RowSweepOrders);
        ApplyChecked(_cellRenderSmoothings, selection.CellRenderSmoothings);
        ApplyChecked(_ballisticParticleModes, selection.BallisticParticleModes);
        ApplyChecked(_ejectionResistanceModes, selection.EjectionResistanceModes);
        ApplyChecked(_particleCollisionModes, selection.ParticleCollisionModes);
        ApplyChecked(_landingSearchModes, selection.LandingSearchModes);
        ApplyChecked(_ejectionImpulseTargetModes, selection.EjectionImpulseTargetModes);
        ApplyChecked(_ejectionDeflectionModes, selection.EjectionDeflectionModes);
        ApplyChecked(_reattachmentModes, selection.ReattachmentModes);
        ApplyChecked(_rasterizationBodyParallelisms, selection.RasterizationBodyParallelisms);
        ApplyChecked(_rasterizationCellParallelisms, selection.RasterizationCellParallelisms);
        ApplyChecked(_ballisticResolutionParallelisms, selection.BallisticResolutionParallelisms);
        ApplyChecked(_renderParallelisms, selection.RenderParallelisms);
        _mode = selection.Mode;
        ApplyChecked(_scenarios, selection.ScenarioNames);
        ApplyChecked(_elements, selection.ElementNames);
        _showActiveChunkDebug = selection.ShowActiveChunkDebug;
        _showActiveRectangleDebug = selection.ShowActiveRectangleDebug;
        _realTimePlayback = selection.RealTimePlayback;
        _repeatCount = Math.Clamp(selection.RepeatCount, MinRepeatCount, MaxRepeatCount);
        _reasonableTestAxis = selection.ReasonableTestAxis;
        _filePrefix = selection.FilePrefix;
    }

    // Combinations = (checked values in the current test axis) x (the 4 ReasonableConfig
    // setups), minus invalid scenarios. Delegates to BenchmarkEstimator, which the CLI's --dry-run
    // flag also uses, so the two never compute this differently.
    private (int Combinations, long TotalFrames) EstimateReasonableRun()
    {
        var estimate = BenchmarkEstimator.Estimate(BuildSelection());
        return (estimate.Combinations, estimate.TotalFrames ?? 0);
    }

    // Combinations = checked World Size x Chunk Size x Active Rectangle x Parallelism x
    // Rasterization Body Parallelism x Rasterization Cell Parallelism x Ballistic Resolution
    // Parallelism x Render Parallelism values x the 4 ReasonableConfig setups, minus invalid scenarios
    private (int Combinations, long TotalFrames) EstimateHeavyRun()
    {
        var estimate = BenchmarkEstimator.Estimate(BuildSelection());
        return (estimate.Combinations, estimate.TotalFrames ?? 0);
    }

    private sealed class AxisSection<T> : IAxisSection
        where T : notnull
    {
        private readonly Dictionary<T, bool> _values;
        private Dictionary<T, bool>? _backup;

        public AxisSection(Dictionary<T, bool> values)
        {
            _values = values;
        }

        public void SaveBackup() => _backup = new Dictionary<T, bool>(_values);

        public void RestoreBackup()
        {
            if (_backup == null)
            {
                return;
            }

            foreach (var key in _values.Keys.ToList())
            {
                _values[key] = _backup.TryGetValue(key, out bool value) && value;
            }

            _backup = null;
        }

        public void SelectAll()
        {
            foreach (var key in _values.Keys.ToList())
            {
                _values[key] = true;
            }
        }

        public int CheckedCount() => _values.Values.Count(v => v);
    }
}
