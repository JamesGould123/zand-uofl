using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zand.App;
using Zand.App.Scenarios;
using Zand.App.UI;

// Run through CLI with:
// Zand.App.exe --config [path to configuration JSON file]
string? configPath = null;

// Dry run mode (--dry-run) prints an estimate for how long the run will take to complete.
bool dryRun = false;

// Parse CLI arguments
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--config" && i + 1 < args.Length)
    {
        configPath = args[++i];
    }
    else if (args[i] == "--dry-run")
    {
        dryRun = true;
    }
}

// Handle dry run
if (dryRun)
{
    if (configPath == null)
    {
        Console.Error.WriteLine("--dry-run requires --config <path>.");
        return 1;
    }

    if (!TryLoadSelection(configPath, out var dryRunSelection, out string? dryRunError))
    {
        Console.Error.WriteLine(dryRunError);
        return 1;
    }

    PrintEstimate(dryRunSelection);
    return 0;
}

// Run via CLI (uses Game1 in order to include full rendering setup)
if (configPath != null)
{
    if (!TryLoadSelection(configPath, out var cliSelection, out string? cliError))
    {
        Console.Error.WriteLine(cliError);
        return 1;
    }

    using var cliGame = new Game1(cliSelection);
    cliGame.Run();
    return Environment.ExitCode;
}

using var game = new Game1();
game.Run();
return 0;

// Matches the JSON contract used for MenuScreen -
// a config file produced by the menu's "Copy Config" button works here as is.
static bool TryLoadSelection(string path, out MenuSelection selection, out string? error)
{
    selection = null!;
    error = null;

    if (!File.Exists(path))
    {
        error = $"Config file not found: {path}";
        return false;
    }

    try
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var loaded = JsonSerializer.Deserialize<MenuSelection>(File.ReadAllText(path), options);
        if (loaded == null)
        {
            error = $"Config file deserialized to null: {path}";
            return false;
        }

        selection = loaded;
        return true;
    }
    catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
    {
        error = $"Failed to load config file {path}: {e.Message}";
        return false;
    }
}

static void PrintEstimate(MenuSelection selection)
{
    var estimate = BenchmarkEstimator.Estimate(selection);
    Console.WriteLine($"Mode: {selection.Mode}");
    Console.WriteLine($"{estimate.Combinations} valid combination(s), {selection.RepeatCount} repeat(s)");

    if (estimate.TotalFrames is { } totalFrames)
    {
        Console.WriteLine($"{totalFrames} frames total");
        double seconds = estimate.EstimatedSeconds ?? 0;
        Console.WriteLine(
            $"Estimated run time: {FormatDuration(seconds)} " +
            $"(assumes ~{BenchmarkEstimator.AssumedFramesPerSecond:F0} FPS)");

        Console.WriteLine($"TOTAL_SECONDS={seconds:F1}");
    }
    else
    {
        Console.WriteLine(estimate.Note ?? "No time estimate available for this mode.");
    }
}

static string FormatDuration(double seconds)
{
    var span = TimeSpan.FromSeconds(seconds);
    return span.TotalHours >= 1
        ? $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s"
        : span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
            : $"{span.Seconds}s";
}
