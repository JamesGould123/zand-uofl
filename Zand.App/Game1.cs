namespace Zand.App;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zand.App.Config;
using Zand.App.Metrics;
using Zand.App.Scenarios;
using Zand.App.UI;
using Zand.Core;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Metrics;
using Zand.Rendering;

public class Game1 : Game
{
    private const string MetricsFolder = "zand-uofl";
    private const bool CollectComponentMetrics = true;
    private const int InfoPanelGap = 4;

    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Stopwatch _stepStopwatch = new();
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly System.TimeSpan _defaultInactiveSleepTime;

    private readonly MenuSelection? _cliSelection;
    private readonly bool _cliMode;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private ScenarioRunner _runner = null!;
    private CellRenderer _cellRenderer = null!;
    private ActiveChunkDebugRenderer _activeChunkDebugRenderer = null!;
    private bool _showActiveChunkDebug;
    private ActiveRectangleDebugRenderer _activeRectangleDebugRenderer = null!;
    private bool _showActiveRectangleDebug;
    private RigidBodyRenderer _rigidBodyRenderer = null!;
    private BallisticParticleRenderer _ballisticParticleRenderer = null!;
    private InfoPanelRenderer _infoPanelRenderer = null!;
    private SpriteFont _font = null!;
    private MetricsCollector _metrics = new();
    private Camera _camera;
    private bool _metricsSaved;
    private int? _lastRunId;
    private string _filePrefix = string.Empty;
    private WorldConfig _worldConfigTemplate;
    private MenuSelection? _lastSelection;
    private GameState _state = GameState.Menu;
    private ImGuiRenderer _imGuiRenderer = null!;
    private MenuScreen _menuScreen = null!;
    private KeyboardState _previousKeyboardState;
    private double? _lastFrameStartMs;
    private double _lastSimStepMs;

    // cliSelection: when present, the menu is skipped and a run is triggered immediately,
    // then the process exits once it completes.
    // Used by Program.cs's --config CLI mode; the GUI path leaves this null.
    public Game1(MenuSelection? cliSelection = null)
    {
        _cliSelection = cliSelection;
        _cliMode = cliSelection != null;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = SimConstants.MenuWidthCells * SimConstants.CellSize;
        _graphics.PreferredBackBufferHeight = SimConstants.MenuHeightCells * SimConstants.CellSize;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _defaultInactiveSleepTime = InactiveSleepTime;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _cellRenderer = new CellRenderer(GraphicsDevice, SimConstants.CellSize);
        _rigidBodyRenderer = new RigidBodyRenderer(GraphicsDevice, SimConstants.CellSize);
        _ballisticParticleRenderer = new BallisticParticleRenderer(GraphicsDevice, SimConstants.CellSize);
        _infoPanelRenderer = new InfoPanelRenderer(GraphicsDevice);
        _font = Content.Load<SpriteFont>("DefaultFont");

        _activeChunkDebugRenderer = new ActiveChunkDebugRenderer(GraphicsDevice, SimConstants.CellSize);
        _activeRectangleDebugRenderer = new ActiveRectangleDebugRenderer(GraphicsDevice, SimConstants.CellSize);

        // This is overwritten every frame after a scenario starts (see UpdateCamera).
        // The world size and default camera position aren't known until a scenario is selected.
        _camera = new Camera
        {
            X = 0,
            Y = 0,
            WidthCells = SimConstants.CameraWidthCells,
            HeightCells = SimConstants.CameraHeightCells
        };

        _worldConfigTemplate = new WorldConfig
        {
            CellsPerMeter = SimConstants.PixelsPerMeter / (float)SimConstants.CellSize,
            DebugCaStepsPerSecond = null
        };

        PhaseTimer.Enabled = CollectComponentMetrics;

        _imGuiRenderer = new ImGuiRenderer(this);
        _imGuiRenderer.RebuildFontAtlas();
        _menuScreen = new MenuScreen();

        if (_cliSelection != null)
        {
            StartRun(_cliSelection);
            if (_runner.AllScenariosCompleted)
            {
                System.Console.Error.WriteLine("No valid combinations for the given config - nothing to run.");
                System.Environment.ExitCode = 1;
                Exit();
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboardState = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        bool escapePressed = keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape);

        if (_state == GameState.Menu)
        {
            _previousKeyboardState = keyboardState;
            base.Update(gameTime);
            return;
        }

        if (escapePressed)
        {
            ReturnToMenu();
            _previousKeyboardState = keyboardState;
            base.Update(gameTime);
            return;
        }

        // Frametime includes simulation step (RB, CA, and coupling), drawing, and presenting.
        // A frame's time isn't known until the next frame starts, so we recorded it here for the previous frame
        double frameStartMs = _frameClock.Elapsed.TotalMilliseconds;
        if (_lastFrameStartMs is { } lastFrameStartMs)
        {
            _metrics.RecordFrame(frameStartMs - lastFrameStartMs, _lastSimStepMs);

            // We don't track the warmup frames (first 5 frames) with the rest of the frames, as they're statistical outliers.
            //
            // Phase averages need to cover the same frames as the manifest's frametime avgs,
            // so the phase timers restart once the warm-up frames have been recorded.
            if (_metrics.CurrentRunFrameCount == MetricsCollector.WarmupFrames)
            {
                PhaseTimer.Reset();
            }
        }

        _lastFrameStartMs = frameStartMs;

        _stepStopwatch.Restart();
        _runner.Sim.Step(SimConstants.SimStepSeconds);
        _stepStopwatch.Stop();
        _lastSimStepMs = _stepStopwatch.Elapsed.TotalMilliseconds;
        UpdateCamera();

        // captures final state of the sim directly before the scenario advances
        if (_runner.CurrentFrameBudget is { } budget && _runner.CurrentFrame + 1 >= budget)
        {
            RecordFinalState();
        }

        bool scenarioAdvanced = _runner.Tick();
        if (!scenarioAdvanced
            && keyboardState.IsKeyDown(Keys.OemMinus)
            && !_previousKeyboardState.IsKeyDown(Keys.OemMinus))
        {
            RecordFinalState();
            scenarioAdvanced = _runner.EndCurrentScenario();
        }

        if (scenarioAdvanced)
        {
            HandleScenarioAdvanced();
        }

        _previousKeyboardState = keyboardState;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_state == GameState.Menu)
        {
            _imGuiRenderer.BeforeLayout(gameTime);
            _menuScreen.Draw(StartRun);
            _imGuiRenderer.AfterLayout();
            base.Draw(gameTime);
            return;
        }

        PhaseTimer.Begin(SimPhase.Render);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _cellRenderer.Draw(_spriteBatch, _runner.Grid, _camera, _runner.CurrentCellRenderSmoothing, _runner.CurrentRenderParallelism);
        if (_showActiveChunkDebug && _runner.CurrentChunkSize != ChunkSize.Disabled)
        {
            _activeChunkDebugRenderer.Draw(_spriteBatch, _runner.Grid, _camera, _runner.CurrentChunkSize.ToCellCount());
        }

        if (_showActiveRectangleDebug
            && _runner.CurrentChunkSize != ChunkSize.Disabled
            && _runner.CurrentActiveRectangleMode == ActiveRectangle.Enabled)
        {
            _activeRectangleDebugRenderer.Draw(_spriteBatch, _runner.Grid, _camera);
        }

        _rigidBodyRenderer.Draw(_spriteBatch, _runner.Physics, _camera, _runner.Grid.Height);
        _ballisticParticleRenderer.Draw(_spriteBatch, _runner.Sim.Ballistics.AirborneParticles, _camera, _runner.Grid.Height);
        RenderSimUi();

        _spriteBatch.End();
        PhaseTimer.End(SimPhase.Render);

        base.Draw(gameTime);
    }

    private static string[] BuildConfigLines(RunConfig config, string progressText) =>
    [
        progressText,
        $"{config.WorldWidth}x{config.WorldHeight}",
        $"{config.CaAlgorithm}",
        $"{config.RbSimulation}",
        $"{config.Coupling}",
        $"{config.LiquidPhysics}",
        $"{config.Gravity}",
        $"{config.TempBodyLifetime}",
        $"{config.TempBodyMerging}",
        $"{config.ChunkSize}",
        $"{config.ActiveRectangleMode}",
        $"{config.ParallelismMode}",
        $"{config.LiquidPressureMode}",
        $"{config.SameTickMoveGuard}",
        $"{config.RowSweepOrder}",
        $"{config.CellRenderSmoothing}",
        $"{config.BallisticParticleMode}",
        $"{config.EjectionResistanceMode}",
        $"{config.ParticleCollisionMode}",
        $"{config.LandingSearchMode}",
        $"{config.EjectionImpulseTargetMode}",
        $"{config.EjectionDeflectionMode}",
        $"{config.ReattachmentMode}",
        $"{config.RasterizationBodyParallelism}",
        $"{config.RasterizationCellParallelism}",
        $"{config.BallisticResolutionParallelism}",
        $"{config.RenderParallelism}"
    ];

    // The build writes the current git commit, status, and diff next to the executable (see Zand.App.csproj),
    // so each session records exactly which the state of the code when it was run.
    private static void CopyBuildInfo(string sessionDir)
    {
        string buildInfoDir = Path.Combine(System.AppContext.BaseDirectory, "buildinfo");
        if (!Directory.Exists(buildInfoDir))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(buildInfoDir))
        {
            File.Copy(file, Path.Combine(sessionDir, Path.GetFileName(file)), overwrite: true);
        }
    }

    private void RenderSimUi()
    {
        if (_runner.CurrentRunConfig is not { } runConfig)
        {
            return;
        }

        string progressText = _runner.CurrentFrameBudget is { } budget
            ? $"{_runner.CurrentFrame} / {budget}"
            : $"{_runner.CurrentFrame} (press '-' to end)";

        var titlePosition = new Vector2(0, 4);
        int titleBoxHeight = _infoPanelRenderer.Draw(_spriteBatch, _font, titlePosition, [runConfig.ScenarioName]);

        var configPosition = new Vector2(0, titlePosition.Y + titleBoxHeight + InfoPanelGap);
        string runCounterText = $"{_runner.CurrentRunNumber} / {_runner.TotalRunCount}";
        _infoPanelRenderer.Draw(_spriteBatch, _font, configPosition, BuildConfigLines(runConfig, progressText), runCounterText);
    }

    private void StartRun(MenuSelection selection)
    {
        _filePrefix = selection.FilePrefix;
        _lastSelection = selection;
        _runner = new ScenarioRunner(
            _worldConfigTemplate,
            selection.WorldSizeOptions,
            selection.CaAlgorithms,
            selection.RbSimulations,
            selection.CouplingModes,
            selection.LiquidPhysicsModes,
            selection.GravityModes,
            selection.TempBodyLifetimes,
            selection.TempBodyMergingChoices,
            selection.ChunkSizes,
            selection.ActiveRectangleModes,
            selection.ParallelismModes,
            selection.LiquidPressureModes,
            selection.SameTickMoveGuards,
            selection.RowSweepOrders,
            selection.CellRenderSmoothings,
            selection.BallisticParticleModes,
            selection.EjectionResistanceModes,
            selection.ParticleCollisionModes,
            selection.LandingSearchModes,
            selection.EjectionImpulseTargetModes,
            selection.EjectionDeflectionModes,
            selection.ReattachmentModes,
            selection.RasterizationBodyParallelisms,
            selection.RasterizationCellParallelisms,
            selection.BallisticResolutionParallelisms,
            selection.RenderParallelisms,
            selection.RepeatCount,
            selection.ApplyPinnedAxes,
            selection.RepeatIndexStart,
            selection.IncludesReasonableConfig);
        if (selection.Mode == ScenarioConfigMode.ByElements)
        {
            _runner.RunElements(selection.ElementNames);
        }
        else if (selection.Mode == ScenarioConfigMode.CoreScenariosReasonable)
        {
            _runner.RunCoreScenariosReasonable(
                selection.ReasonableTestAxis, selection.UseHeavyScenario, selection.UseBodyStressScenario);
        }
        else if (selection.Mode == ScenarioConfigMode.HeavyBenchmark)
        {
            _runner.RunHeavyBenchmark();
        }
        else
        {
            _runner.RunSelected(selection.ScenarioNames);
        }

        _showActiveChunkDebug = selection.ShowActiveChunkDebug;
        _showActiveRectangleDebug = selection.ShowActiveRectangleDebug;

        _metrics = new MetricsCollector(selection.RealTimePlayback);
        _metricsSaved = false;
        _lastRunId = null;
        BeginMetricsForCurrentScenario();

        _state = _runner.AllScenariosCompleted ? GameState.Menu : GameState.Running;
        ResizeWindow(
            _state == GameState.Menu ? SimConstants.MenuWidthCells : SimConstants.CameraWidthCells,
            _state == GameState.Menu ? SimConstants.MenuHeightCells : SimConstants.CameraHeightCells);
        SetBenchmarkTiming(_state == GameState.Running && !selection.RealTimePlayback);
    }

    private void HandleScenarioAdvanced()
    {
        BeginMetricsForCurrentScenario();
        if (_runner.AllScenariosCompleted)
        {
            FinishRun();
        }
    }

    private void ReturnToMenu()
    {
        FlushCurrentPhaseSnapshot();
        FinishRun();
    }

    // Run can be finished in two ways: the scenario queue completing or the user pressing Escape.
    // In CLI mode this exits the process,
    // otherwise it changes to the menu mode.
    private void FinishRun()
    {
        if (!_metricsSaved)
        {
            string sessionDir = SaveMetricsToDisk();
            if (_cliMode)
            {
                System.Console.WriteLine($"SESSION_DIR={sessionDir}");
            }
        }

        if (_cliMode)
        {
            Exit();
            return;
        }

        _state = GameState.Menu;
        ResizeWindow(SimConstants.MenuWidthCells, SimConstants.MenuHeightCells);
        SetBenchmarkTiming(false);
    }

    // Benchmark runs go as fast as they can instead of aiming for a fixed 60 Hz update rate.
    // They also have no vsync and no sleeping while the window is unfocused,
    // since both could affect recorded frametime in test runs.
    // High priority prevents the focused window's scheduling boost from favouring one run over another.
    //
    // The simulation when run via the menu still advances by SimStepSeconds per step.
    private void SetBenchmarkTiming(bool enabled)
    {
        IsFixedTimeStep = !enabled;
        _graphics.SynchronizeWithVerticalRetrace = !enabled;
        _graphics.ApplyChanges();
        InactiveSleepTime = enabled ? System.TimeSpan.Zero : _defaultInactiveSleepTime;
        Process.GetCurrentProcess().PriorityClass = enabled ? ProcessPriorityClass.High : ProcessPriorityClass.Normal;
    }

    // Used to change window size between the menu and the simulation view.
    private void ResizeWindow(int widthCells, int heightCells)
    {
        int widthPixels = widthCells * SimConstants.CellSize;
        int heightPixels = heightCells * SimConstants.CellSize;
        if (_graphics.PreferredBackBufferWidth == widthPixels && _graphics.PreferredBackBufferHeight == heightPixels)
        {
            return;
        }

        _graphics.PreferredBackBufferWidth = widthPixels;
        _graphics.PreferredBackBufferHeight = heightPixels;
        _graphics.ApplyChanges();
    }

    private void UpdateCamera()
    {
        var pan = _runner.CurrentCameraPan;
        if (pan == null)
        {
            _camera.X = _runner.CurrentDefaultCameraX;
            _camera.Y = _runner.CurrentDefaultCameraY;
            return;
        }

        float t = _runner.CurrentFrameBudget is int budget && budget > 0
            ? (float)_runner.CurrentFrame / budget
            : 0f;
        _camera.X = pan.StartX + ((pan.EndX - pan.StartX) * t);
        _camera.Y = pan.StartY + ((pan.EndY - pan.StartY) * t);
    }

    private void BeginMetricsForCurrentScenario()
    {
        // The gap between the last frame of one run and the first of the next includes scenario
        // setup, so it isn't a real frame and is never recorded.
        _lastFrameStartMs = null;
        FlushCurrentPhaseSnapshot();
        if (_runner.AllScenariosCompleted)
        {
            return;
        }

        _lastRunId = _metrics.BeginScenario(_runner.CurrentRunConfig!);

        if (_cliMode)
        {
            // Lets the batch runner report which run it was on if it hangs or crashes.
            var config = _runner.CurrentRunConfig!;
            System.Console.WriteLine(
                $"RUN_START {_runner.CurrentRunNumber}/{_runner.TotalRunCount} {config.ScenarioName} repeat={config.RepeatIndex} " +
                $"world={config.WorldWidth} ca={config.CaAlgorithm} rb={config.RbSimulation} coupling={config.Coupling} " +
                $"liquid={config.LiquidPhysics} gravity={config.Gravity} chunk={config.ChunkSize} " +
                $"activeRect={config.ActiveRectangleMode} parallelism={config.ParallelismMode} ballistics={config.BallisticParticleMode}");
        }
    }

    // Records state of a scenario right, called right before it ends
    private void RecordFinalState()
    {
        _metrics.RecordFinalState(FinalStateRecorder.Capture(_runner.CurrentFrame + 1, _runner.Grid, _runner.Physics));
    }

    private void FlushCurrentPhaseSnapshot()
    {
        if (CollectComponentMetrics && _lastRunId is { } runId)
        {
            _metrics.AddPhaseSnapshot(runId, PhaseTimer.Snapshot());
            PhaseTimer.Reset();
        }
    }

    private string SaveMetricsToDisk()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string prefix = SanitizeFileNamePart(_filePrefix);
        string folderName = prefix.Length > 0 ? $"{prefix}_{timestamp}" : timestamp;

        string sessionDir = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            MetricsFolder,
            "runs",
            folderName);
        _metrics.Save(sessionDir);
        if (_lastSelection != null)
        {
            File.WriteAllText(
                Path.Combine(sessionDir, "config.json"),
                JsonSerializer.Serialize(_lastSelection, ConfigJsonOptions));
        }

        CopyBuildInfo(sessionDir);

        _metricsSaved = true;
        return sessionDir;
    }

    private string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Where(c => !invalidChars.Contains(c)).ToArray();
        return new string(chars);
    }
}
