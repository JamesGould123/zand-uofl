"""Run the entire testing plan: build Zand.App, generate one MenuSelection
config per axis from axis_test_plan.json, run each through the CLI (Zand.App --config ...), feed
every result into axis_significance_test.py, re-run the first axis again at the end as a drift
check, and write one final summary - printed and saved to disk.

Usage:
    .venv/Scripts/python run_testing_plan.py [--skip-build] [--axes AXIS [AXIS ...]] [--dry-run-only]
                                              [--timeout-factor N]
"""

import argparse
import copy
import json
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime
from pathlib import Path

import pandas as pd

REPO_ROOT = Path(__file__).resolve().parent.parent
ZAND_APP_CSPROJ = REPO_ROOT / "Zand.App" / "Zand.App.csproj"
ZAND_APP_EXE = REPO_ROOT / "Zand.App" / "bin" / "Release" / "net9.0" / "Zand.App.exe"
SCRIPTS_DIR = Path(__file__).resolve().parent
AXIS_TEST_PLAN = SCRIPTS_DIR / "axis_test_plan.json"
SCRIPTS_SNAPSHOT_DIR_NAME = "scripts-snapshot"
ANALYSIS_SCRIPT = Path(__file__).resolve().parent / "axis_significance_test.py"
RUNS_DIR = Path.home() / "Documents" / "zand-uofl" / "runs"

SESSION_DIR_PATTERN = re.compile(r"^SESSION_DIR=(.+)$", re.MULTILINE)
TOTAL_SECONDS_PATTERN = re.compile(r"^TOTAL_SECONDS=([\d.]+)$", re.MULTILINE)
SIGNIFICANT_PATTERN = re.compile(r"->\s*SIGNIFICANT")
NOT_SIGNIFICANT_PATTERN = re.compile(r"->\s*not significant|nothing to test")
DEFAULT_METRIC = "avg_frame_ms"
ALWAYS_METRICS = [DEFAULT_METRIC, "avg_sim_step_ms", "phase_render_avg_ms"]
RUN_START_PATTERN = re.compile(r"^RUN_START (.+)$", re.MULTILINE)
FAILED_CONFIGS_DIR = RUNS_DIR / "failed-configs"
TIMEOUT_STARTUP_SECONDS = 60


def load_plan() -> dict:
    with open(AXIS_TEST_PLAN, encoding="utf-8") as f:
        return json.load(f)


def heavy_reasonable_plan(plan: dict) -> dict:
    heavy = copy.deepcopy(plan)
    heavy["session_prefix"] = "testing-plan-heavy"
    heavy["defaults"]["UseHeavyScenario"] = True
    heavy["defaults"]["WorldSizeOptions"] = ["Large2048"]
    heavy["axes"] = []
    for entry in plan["axes"]:
        if entry["mode"] != "CoreScenariosReasonable":
            continue

        heavy_entry = copy.deepcopy(entry)
        pairings = heavy_entry.get("pairings") or [{"when": list(heavy_entry["values"]), "set": {}}]
        for pairing in pairings:
            pairing["set"]["WorldSizeOptions"] = ["Large2048"]

        heavy_entry["pairings"] = pairings
        heavy["axes"].append(heavy_entry)

    # Needed to translate pinned MenuSelection fields back to axis names, including WorldSize.
    heavy["axis_fields"] = {a["menu_selection_field"]: a["axis"] for a in plan["axes"]}
    return heavy


LIGHT_PARALLELISM_AXES = [
    "RasterizationBodyParallelism", "RasterizationCellParallelism",
    "BallisticResolutionParallelism", "RenderParallelism",
]


def light_parallelism_plan(plan: dict) -> dict:
    light = copy.deepcopy(plan)
    light["session_prefix"] = "testing-plan-light"
    light["axes"] = []
    for entry in plan["axes"]:
        if entry["axis"] not in LIGHT_PARALLELISM_AXES:
            continue

        light_entry = copy.deepcopy(entry)
        light_entry["mode"] = "CoreScenariosReasonable"
        light["axes"].append(light_entry)

    light["axis_fields"] = {a["menu_selection_field"]: a["axis"] for a in plan["axes"]}
    return light


def body_stress_parallelism_plan(plan: dict) -> dict:
    bodies = light_parallelism_plan(plan)
    bodies["session_prefix"] = "testing-plan-bodies"
    bodies["defaults"]["UseBodyStressScenario"] = True
    bodies["defaults"]["WorldSizeOptions"] = ["Medium1024"]
    for entry in bodies["axes"]:
        pairings = entry.get("pairings") or [{"when": list(entry["values"]), "set": {}}]
        for pairing in pairings:
            pairing["set"]["WorldSizeOptions"] = ["Medium1024"]

        entry["pairings"] = pairings

    return bodies


def axis_groups(entry: dict) -> list[dict]:
    pairings = entry.get("pairings")
    if not pairings:
        return [{"when": entry["values"], "set": {}}]

    covered = [v for pairing in pairings for v in pairing["when"]]
    if sorted(covered) != sorted(entry["values"]):
        sys.exit(
            f"{entry['axis']}: pairings must cover every value exactly once. "
            f"Values: {entry['values']}, covered by pairings: {covered}")

    return pairings


def build_selection(plan: dict, axis_entry: dict, group: dict, repeat_count: int | None = None,
                    repeat_index_start: int = 0, prefix_suffix: str = "") -> dict:
    selection = dict(plan["defaults"])
    selection.update(axis_entry.get("overrides", {}))
    selection[axis_entry["menu_selection_field"]] = group["when"]

    for field, values in group["set"].items():
        if field not in plan["defaults"]:
            sys.exit(f"{axis_entry['axis']}: pairing sets unknown MenuSelection field '{field}'.")

        if field == axis_entry["menu_selection_field"]:
            sys.exit(f"{axis_entry['axis']}: a pairing can't set the tested axis's own field '{field}'.")

        selection[field] = values

    selection["Mode"] = axis_entry["mode"]
    selection["ScenarioNames"] = []
    selection["ElementNames"] = []
    selection["ShowActiveChunkDebug"] = False
    selection["ShowActiveRectangleDebug"] = False
    selection["RepeatCount"] = plan["repeat_count"] if repeat_count is None else repeat_count
    selection["RepeatIndexStart"] = repeat_index_start
    selection["ReasonableTestAxis"] = axis_entry["axis"]
    selection["FilePrefix"] = f"{plan.get('session_prefix', 'testing-plan')}-{axis_entry['axis']}{prefix_suffix}"

    axis_by_field = plan.get("axis_fields") or {a["menu_selection_field"]: a["axis"] for a in plan["axes"]}
    if axis_entry["mode"] == "CoreScenariosReasonable" and group["set"]:
        selection["PinnedAxes"] = [axis_by_field[field] for field in group["set"]]

    config_filter = axis_entry.get("config_filter")
    if config_filter:
        if axis_entry["mode"] != "CoreScenariosReasonable":
            sys.exit(f"{axis_entry['axis']}: config_filter is only supported in CoreScenariosReasonable mode.")

        for field, values in config_filter.items():
            if field not in axis_by_field:
                sys.exit(f"{axis_entry['axis']}: config_filter names unknown MenuSelection field '{field}'.")

            if field == axis_entry["menu_selection_field"] or field in group["set"]:
                sys.exit(f"{axis_entry['axis']}: config_filter field '{field}' conflicts with the tested axis or a pairing.")

            selection[field] = values

        selection["RestrictedAxes"] = [axis_by_field[field] for field in config_filter]

    return selection


def axis_invocations(plan: dict, entry: dict, name_suffix: str = "") -> list[tuple[str, dict]]:
    groups = axis_groups(entry)
    axis = entry["axis"]
    if len(groups) == 1:
        return [(f"{axis}{name_suffix}", build_selection(plan, entry, groups[0], prefix_suffix=name_suffix))]

    invocations = []
    for repeat in range(plan["repeat_count"]):
        order = list(enumerate(groups))
        if repeat % 2 == 1:
            order.reverse()

        for group_index, group in order:
            suffix = f"{name_suffix}-g{group_index}-r{repeat}"
            selection = build_selection(
                plan, entry, group, repeat_count=1, repeat_index_start=repeat, prefix_suffix=suffix)
            invocations.append((f"{axis}{suffix}", selection))

    return invocations


def write_config(selection: dict, tmp_dir: Path, name: str) -> Path:
    path = tmp_dir / f"{name}.json"
    path.write_text(json.dumps(selection, indent=2), encoding="utf-8")
    return path


def build_zand_app() -> None:
    print(f"Building {ZAND_APP_CSPROJ} (Release)...")
    result = subprocess.run(
        ["dotnet", "build", str(ZAND_APP_CSPROJ), "-c", "Release"],
        cwd=REPO_ROOT, capture_output=True, text=True,
    )
    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr, file=sys.stderr)
        sys.exit(f"Build failed (exit {result.returncode}) - see output above.")

    if not ZAND_APP_EXE.is_file():
        sys.exit(f"Build reported success but {ZAND_APP_EXE} doesn't exist - check the output path.")

    print("Build OK.\n")


def dry_run(config_path: Path) -> tuple[str, float | None]:
    result = subprocess.run(
        [str(ZAND_APP_EXE), "--dry-run", "--config", str(config_path)],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        sys.exit(f"--dry-run failed for {config_path}:\n{result.stdout}\n{result.stderr}")

    match = TOTAL_SECONDS_PATTERN.search(result.stdout)
    seconds = float(match.group(1)) if match else None
    return result.stdout, seconds


def snapshot_scripts(session_dir: str) -> None:
    destination = Path(session_dir) / SCRIPTS_SNAPSHOT_DIR_NAME
    destination.mkdir(parents=True, exist_ok=True)
    for pattern in ("*.py", "*.json"):
        for source in SCRIPTS_DIR.glob(pattern):
            shutil.copy2(source, destination / source.name)


def run_invocation(config_path: Path, name: str, timeout_seconds: float | None) -> tuple[str | None, dict | None]:
    limit_text = f", timeout {timeout_seconds / 60:.1f} min" if timeout_seconds else ""
    print(f"Running {name} ({config_path.name}{limit_text})...")
    process = subprocess.Popen(
        [str(ZAND_APP_EXE), "--config", str(config_path)],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
    )
    reason = None
    try:
        stdout, stderr = process.communicate(timeout=timeout_seconds)
    except subprocess.TimeoutExpired:
        process.kill()
        stdout, stderr = process.communicate()
        reason = f"timed out after {timeout_seconds / 60:.1f} min"

    if stderr:
        print(stderr, file=sys.stderr)

    match = SESSION_DIR_PATTERN.search(stdout)
    if reason is None and process.returncode != 0:
        reason = f"exited with code {process.returncode}"

    if reason is None and not match:
        reason = "finished without printing a SESSION_DIR line"

    if reason is None:
        session_dir = match.group(1).strip()
        print(f"  -> {session_dir}")
        snapshot_scripts(session_dir)
        return session_dir, None

    run_starts = RUN_START_PATTERN.findall(stdout)
    FAILED_CONFIGS_DIR.mkdir(parents=True, exist_ok=True)
    saved_config = FAILED_CONFIGS_DIR / f"{name}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    shutil.copy(config_path, saved_config)
    failure = {
        "invocation": name,
        "reason": reason,
        "last_run": run_starts[-1] if run_starts else "(no run had started)",
        "config": str(saved_config),
    }
    print(f"  FAILED: {reason}. Last run started: {failure['last_run']}")
    return None, failure


def metrics_for(entry: dict) -> list[str]:
    return ALWAYS_METRICS + [m for m in entry.get("metrics", []) if m not in ALWAYS_METRICS]


def run_analysis(entry: dict, session_dirs: list[str], metric: str = DEFAULT_METRIC) -> str:
    command = [sys.executable, str(ANALYSIS_SCRIPT), entry["manifest_column"], *session_dirs, "--metric", metric]
    ignore_columns = entry.get("ignore_columns")
    if ignore_columns:
        command += ["--ignore-axis", *ignore_columns]

    result = subprocess.run(command, capture_output=True, text=True)
    output = result.stdout + result.stderr
    if result.returncode != 0:
        print(f"  (analysis for {entry['manifest_column']} exited {result.returncode})", file=sys.stderr)

    return output


def verdict_of(analysis_output: str) -> str:
    if SIGNIFICANT_PATTERN.search(analysis_output):
        return "SIGNIFICANT"

    # "not significant" only when a test actually ran (or every pair was identical).
    # If the analysis couldn't run at all (for example no complete blocks),
    # that must not read as "no difference".
    if NOT_SIGNIFICANT_PATTERN.search(analysis_output):
        return "not significant"

    return "NO RESULT (no test ran - see analysis details)"


def median_by_axis_value(session_dirs: list[str], axis_column: str, metric: str = "avg_frame_ms") -> pd.Series:
    frames = [
        pd.read_csv(Path(d) / "manifest.csv", keep_default_na=False, na_values=[""])
        for d in session_dirs
    ]
    return pd.concat(frames, ignore_index=True).groupby(axis_column)[metric].median()


def run_invocations(invocations: list[tuple[str, dict]], tmp_dir: Path, label: str,
                    timeout_factor: float) -> tuple[list[str], dict | None]:
    session_dirs = []
    for name, selection in invocations:
        config_path = write_config(selection, tmp_dir, name)
        timeout_seconds = None
        if timeout_factor > 0:
            _, estimated_seconds = dry_run(config_path)
            if estimated_seconds is not None:
                timeout_seconds = (estimated_seconds * timeout_factor) + TIMEOUT_STARTUP_SECONDS

        session_dir, failure = run_invocation(config_path, f"{label} {name}".strip(), timeout_seconds)
        if failure is not None:
            return session_dirs, failure

        session_dirs.append(session_dir)

    return session_dirs, None


def drift_check(plan: dict, first_axis_entry: dict, tmp_dir: Path, first_session_dirs: list[str],
                timeout_factor: float) -> str:
    print(f"\nRunning drift check: re-running {first_axis_entry['axis']} again...")
    invocations = axis_invocations(plan, first_axis_entry, name_suffix="-drift-check")
    second_session_dirs, failure = run_invocations(invocations, tmp_dir, "(drift check)", timeout_factor)
    if failure is not None:
        return (f"## Drift check: {first_axis_entry['axis']}\n\nSkipped: the re-run failed "
                f"({failure['reason']}; last run started: {failure['last_run']}).")

    axis_column = first_axis_entry["manifest_column"]
    first_medians = median_by_axis_value(first_session_dirs, axis_column)
    second_medians = median_by_axis_value(second_session_dirs, axis_column)

    first_analysis = run_analysis(first_axis_entry, first_session_dirs)
    second_analysis = run_analysis(first_axis_entry, second_session_dirs)
    first_verdict = verdict_of(first_analysis)
    second_verdict = verdict_of(second_analysis)

    lines = [
        f"## Drift check: {first_axis_entry['axis']}, re-run at the end of the batch",
        "",
        f"First run verdict: {first_verdict}. Repeat run verdict: {second_verdict}."
        + (" **VERDICT CHANGED - investigate.**" if first_verdict != second_verdict else " (unchanged)"),
        "",
        "| Value | First run median (ms) | Repeat run median (ms) | Change |",
        "|---|---|---|---|",
    ]
    for value in first_medians.index:
        first_val = first_medians.get(value)
        second_val = second_medians.get(value)
        if first_val is None or second_val is None:
            lines.append(f"| {value} | {first_val} | {second_val} | (missing in one run) |")
            continue

        pct = ((second_val - first_val) / first_val * 100) if first_val else 0.0
        lines.append(f"| {value} | {first_val:.3f} | {second_val:.3f} | {pct:+.1f}% |")

    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--skip-build", action="store_true", help="Skip dotnet build, use the existing Zand.App.exe")
    parser.add_argument("--axes", nargs="+", default=None, help="Only run these axes (by name), default: all")
    parser.add_argument("--dry-run-only", action="store_true", help="Print time estimates for every axis, then stop")
    parser.add_argument(
        "--timeout-factor", type=float, default=3.0,
        help="Kill an invocation after its --dry-run estimate times this, plus a minute (default 3, 0 = no timeout)")
    parser.add_argument(
        "--heavy-reasonable", action="store_true",
        help="Run the Core Scenarios axes on the heavy benchmark scenario at 2048x2048 instead (see heavy_reasonable_plan)")
    parser.add_argument(
        "--light-parallelism", action="store_true",
        help="Run the heavy-only parallelism axes on the core scenarios instead (see light_parallelism_plan)")
    parser.add_argument(
        "--body-stress-parallelism", action="store_true",
        help="Run the heavy-only parallelism axes on the body stress scenario instead (see body_stress_parallelism_plan)")
    parser.add_argument("--repeat-count", type=int, default=None,
                        help="Override the plan's repeat_count (an even number lets every value run first and last equally often)")
    args = parser.parse_args()

    plan = load_plan()
    if sum([args.heavy_reasonable, args.light_parallelism, args.body_stress_parallelism]) > 1:
        sys.exit("--heavy-reasonable, --light-parallelism and --body-stress-parallelism can't be combined.")

    if args.heavy_reasonable:
        plan = heavy_reasonable_plan(plan)
    elif args.light_parallelism:
        plan = light_parallelism_plan(plan)
    elif args.body_stress_parallelism:
        plan = body_stress_parallelism_plan(plan)

    if args.repeat_count is not None:
        plan["repeat_count"] = args.repeat_count
    axis_entries = plan["axes"]
    if args.axes:
        selected = set(args.axes)
        axis_entries = [a for a in axis_entries if a["axis"] in selected]
        missing = selected - {a["axis"] for a in axis_entries}
        if missing:
            sys.exit(f"Unknown axis name(s): {', '.join(sorted(missing))}")

    if not axis_entries:
        sys.exit("No axes selected.")

    if not args.skip_build and not args.dry_run_only:
        build_zand_app()
    elif not ZAND_APP_EXE.is_file():
        sys.exit(f"{ZAND_APP_EXE} doesn't exist - run without --skip-build first, or build it yourself.")

    with tempfile.TemporaryDirectory(prefix="zand-testing-plan-") as tmp_dir_str:
        tmp_dir = Path(tmp_dir_str)

        print(f"Estimating {len(axis_entries)} axis run(s)...\n")
        total_seconds = 0.0
        unestimated_axes = []
        for entry in axis_entries:
            for group_index, group in enumerate(axis_groups(entry)):
                selection = build_selection(plan, entry, group)
                config_path = write_config(selection, tmp_dir, f"{entry['axis']}_estimate{group_index}")
                estimate_text, seconds = dry_run(config_path)
                print(f"[{entry['axis']}, group {group_index}]\n{estimate_text}")
                if seconds is None:
                    unestimated_axes.append(entry["axis"])
                else:
                    total_seconds += seconds

        print(f"Estimated total for {len(axis_entries)} axis run(s): ~{total_seconds / 60:.1f} min"
              f" (plus the drift check, which re-runs axis #1)")
        if unestimated_axes:
            print(f"No time estimate available for: {', '.join(unestimated_axes)} (see each axis's note above)")

        if args.dry_run_only:
            return

        results = []
        failures = []
        for entry in axis_entries:
            session_dirs, failure = run_invocations(
                axis_invocations(plan, entry), tmp_dir, "", args.timeout_factor)
            if failure is not None:
                print(f"{entry['axis']} FAILED, skipping its analysis and moving on.\n")
                failures.append((entry, failure, session_dirs))
                continue

            analyses = []
            for metric in metrics_for(entry):
                analysis_output = run_analysis(entry, session_dirs, metric)
                print(f"--- {entry['axis']} on {metric} ---")
                print(analysis_output)
                analyses.append((metric, verdict_of(analysis_output), analysis_output))

            results.append((entry, session_dirs, analyses))

        if results:
            drift_report = drift_check(plan, results[0][0], tmp_dir, results[0][1], args.timeout_factor)
        else:
            drift_report = "## Drift check\n\nSkipped: no axis completed."

        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        RUNS_DIR.mkdir(parents=True, exist_ok=True)
        summary_path = RUNS_DIR / f"{plan.get('session_prefix', 'testing-plan')}-summary_{timestamp}.md"

        lines = [
            f"# Testing plan summary - {timestamp}",
            "",
            "| Axis | Mode | Verdicts (per metric) | Session folder(s) |",
            "|---|---|---|---|",
        ]
        for entry, session_dirs, analyses in results:
            verdicts = "<br>".join(f"{metric}: {verdict}" for metric, verdict, _ in analyses)
            folders = ", ".join(f"`{d}`" for d in session_dirs)
            lines.append(f"| {entry['axis']} | {entry['mode']} | {verdicts} | {folders} |")

        for entry, failure, partial_dirs in failures:
            folders = ", ".join(f"`{d}`" for d in partial_dirs) or "(none finished)"
            lines.append(f"| {entry['axis']} | {entry['mode']} | **FAILED**: {failure['reason']} | {folders} |")

        lines.append("")
        lines.append(drift_report)

        if failures:
            lines.append("")
            lines.append("## Failed axes")
            lines.append("")
            lines.append("Each failed axis was skipped (no analysis). Re-run one on its own with "
                         "`.venv/Scripts/python run_testing_plan.py --skip-build --axes <Axis>`.")
            for entry, failure, _ in failures:
                lines.extend([
                    "",
                    f"### {entry['axis']}",
                    "",
                    f"- Invocation: `{failure['invocation']}`",
                    f"- Reason: {failure['reason']}",
                    f"- Last run started: `{failure['last_run']}`",
                    f"- Config that failed (saved copy): `{failure['config']}`",
                ])

        lines.append("")
        lines.append("## Analysis details")
        for entry, _, analyses in results:
            for metric, _, analysis_output in analyses:
                lines.extend(["", f"### {entry['axis']} - {metric}", "", "```", analysis_output.strip(), "```"])

        report = "\n".join(lines)
        summary_path.write_text(report, encoding="utf-8")
        print(f"\n{report}\n\nSaved to {summary_path}")
        if failures:
            sys.exit(f"{len(failures)} axis run(s) failed: {', '.join(e['axis'] for e, _, _ in failures)}.")


if __name__ == "__main__":
    main()
