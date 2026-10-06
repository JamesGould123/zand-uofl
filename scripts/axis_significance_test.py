"""Test whether an axis's options make a statistically significant difference,
using manifest.csv output - paired (Friedman / Wilcoxon signed-rank).

Usage:
    .venv/Scripts/python axis_significance_test.py <axis> <folder> [<folder> ...] [--metric COLUMN] [--alpha 0.05]
        [--ignore-axis COLUMN [COLUMN ...]]
"""

import argparse
import itertools
import sys
from pathlib import Path

import pandas as pd
from scipy import stats

AXIS_COLUMNS = [
    "scenario", "world_width", "world_height", "ca_algorithm", "rb_simulation",
    "coupling", "liquid_physics", "gravity", "temp_body_lifetime", "temp_body_merging", "chunk_size",
    "active_rectangle", "parallelism", "liquid_pressure", "same_tick_move_guard",
    "row_sweep_order", "cell_render_smoothing", "ballistics",
    "ejection_resistance", "particle_collision", "landing_search", "impulse_target",
    "deflection", "reattachment", "rasterization_body_parallelism", "rasterization_cell_parallelism",
    "ballistic_resolution_parallelism", "render_parallelism",
]

AXIS_ALIASES: dict[str, str] = {
    "chunking_setting": "chunk_size",
}

AXIS_VALUE_ORDER: dict[str, list[str]] = {
    "chunk_size": ["Disabled", "Eight", "Sixteen", "ThirtyTwo", "SixtyFour", "OneTwentyEight"],
}

def find_manifests(folder: Path) -> list[Path]:
    direct = folder / "manifest.csv"
    if direct.is_file():
        return [direct]

    found = sorted(folder.glob("**/manifest.csv"))
    if not found:
        sys.exit(f"No manifest.csv found at {folder} or in any subfolder of it.")

    return found


def load_manifest(folders: list[Path]) -> pd.DataFrame:
    manifests = [m for folder in folders for m in find_manifests(folder)]
    frames = []
    for path in manifests:
        # keep_default_na=False stops pandas reading the literal enum value "None"
        # (CouplingMode.None, LiquidPhysicsMode.None, ...) as a missing value - only
        # genuinely empty fields (blank phase columns for phases a run never hit) are NaN.
        frame = pd.read_csv(path, keep_default_na=False, na_values=[""])
        frame["_session"] = path.parent.name
        frames.append(frame)

    print(f"Loaded {len(frames)} manifest(s), {sum(len(f) for f in frames)} run(s) total:")
    for path in manifests:
        print(f"  {path}")

    return pd.concat(frames, ignore_index=True)


def add_level_column(df: pd.DataFrame, axis: str, ignored: list[str]) -> str:
    if not ignored:
        return axis

    suffix = df[ignored].astype(str).apply(
        lambda row: ", ".join(f"{column}={value}" for column, value in row.items()), axis=1)
    df["_level"] = df[axis].astype(str) + " (" + suffix + ")"
    return "_level"


def ordered_values(df: pd.DataFrame, axis: str, level_col: str) -> list:
    present_axis_values = list(df[axis].unique())
    order = AXIS_VALUE_ORDER.get(axis)
    if order is None:
        axis_values = sorted(present_axis_values, key=str)
    else:
        extra = [v for v in present_axis_values if v not in order]
        axis_values = [v for v in order if v in present_axis_values] + extra

    if level_col == axis:
        return axis_values

    levels = []
    for value in axis_values:
        levels.extend(sorted(df.loc[df[axis] == value, level_col].unique()))

    return levels


def build_blocks(df: pd.DataFrame, axis: str, level_col: str, ignored: list[str], metric: str) -> pd.DataFrame:
    block_columns = [c for c in AXIS_COLUMNS if c != axis and c not in ignored and c in df.columns]
    if "repeat_index" in df.columns:
        block_columns.append("repeat_index")

    duplicate_check = df.duplicated(subset=block_columns + [level_col])
    if duplicate_check.any():
        print(
            f"Note: {duplicate_check.sum()} duplicate (block, {level_col}) row(s) found - "
            "averaging them together. This is expected if repeat_index wasn't recorded "
            "for these runs (older manifests); otherwise check the data.",
        )

    collapsed = df.groupby(block_columns + [level_col], dropna=False)[metric].mean().reset_index()
    pivoted = collapsed.pivot_table(index=block_columns, columns=level_col, values=metric)

    complete = pivoted.dropna(axis=0, how="any")
    dropped = len(pivoted) - len(complete)
    if dropped:
        print(
            f"Dropped {dropped}/{len(pivoted)} block(s) missing a run for at least one "
            f"'{axis}' option (incomplete blocks can't be used in a paired test).",
        )

    return complete


def holm_bonferroni(pairs: list[tuple], p_values: list[float]) -> list[tuple]:
    order = sorted(range(len(p_values)), key=lambda i: p_values[i])
    m = len(p_values)
    results = [None] * m
    running_max = 0.0
    for rank, i in enumerate(order):
        adjusted = min((m - rank) * p_values[i], 1.0)
        running_max = max(running_max, adjusted)  # enforces monotonicity, standard Holm step-up
        results[i] = (pairs[i], p_values[i], running_max)

    return [(pair, raw, corrected, corrected < 0.05) for pair, raw, corrected in results]


def run_two_value_test(blocks: pd.DataFrame, axis: str, values: list) -> None:
    a, b = values
    x, y = blocks[a].to_numpy(), blocks[b].to_numpy()
    print(f"\nExactly two '{axis}' values present ({a!r} vs {b!r}) - running Wilcoxon signed-rank test.")
    print(f"n = {len(blocks)} matched block(s). Medians: {a!r}={pd.Series(x).median():.3f}  {b!r}={pd.Series(y).median():.3f}")

    if (x == y).all():
        print("Every matched pair is identical - nothing to test (p = 1.0).")
        return

    try:
        result = stats.wilcoxon(x, y)
    except ValueError as e:
        print(f"Could not run Wilcoxon test: {e}")
        return

    verdict = "SIGNIFICANT (p < 0.05)" if result.pvalue < 0.05 else "not significant"
    print(f"Wilcoxon signed-rank: statistic={result.statistic:.3f}, p={result.pvalue:.5f} -> {verdict}")


def run_multi_value_test(blocks: pd.DataFrame, axis: str, values: list, alpha: float) -> None:
    print(f"\n{len(values)} '{axis}' values present - running Friedman test first (omnibus check).")
    print(f"n = {len(blocks)} matched block(s).")
    for v in values:
        print(f"  {v!r}: median={blocks[v].median():.3f} ms")

    columns = [blocks[v].to_numpy() for v in values]
    try:
        result = stats.friedmanchisquare(*columns)
    except ValueError as e:
        print(f"Could not run Friedman test: {e}")
        return

    verdict = "SIGNIFICANT" if result.pvalue < alpha else "not significant"
    print(f"Friedman: statistic={result.statistic:.3f}, p={result.pvalue:.5f} -> {verdict} at alpha={alpha}")

    if result.pvalue >= alpha:
        print(
            "Friedman found no overall difference, so no pairwise Wilcoxon tests are run "
            "(running them anyway would just inflate false positives on noise).",
        )
        return

    print("\nFriedman was significant - running pairwise Wilcoxon signed-rank tests (Holm-Bonferroni corrected):")
    pairs = list(itertools.combinations(values, 2))
    p_values = []
    for a, b in pairs:
        x, y = blocks[a].to_numpy(), blocks[b].to_numpy()
        if (x == y).all():
            p_values.append(1.0)
            continue

        try:
            p_values.append(stats.wilcoxon(x, y).pvalue)
        except ValueError:
            p_values.append(1.0)

    for (a, b), raw, corrected, significant in holm_bonferroni(pairs, p_values):
        verdict = "SIGNIFICANT" if significant else "not significant"
        print(f"  {a!r} vs {b!r}: raw p={raw:.5f}, Holm-corrected p={corrected:.5f} -> {verdict}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    alias_help = ", ".join(f"{alias} -> {col}" for alias, col in AXIS_ALIASES.items())
    parser.add_argument("axis", help=f"Configuration axis to test. One of: {', '.join(AXIS_COLUMNS)}. Aliases: {alias_help}")
    parser.add_argument(
        "folder", type=Path, nargs="+",
        help="Session folder(s) (each has manifest.csv) or parent(s) of many. Put options like --ignore-axis after these.")
    parser.add_argument("--metric", default="avg_frame_ms", help="Manifest column to compare (default: avg_frame_ms)")
    parser.add_argument("--alpha", type=float, default=0.05, help="Significance threshold for the Friedman omnibus check (default: 0.05)")
    parser.add_argument(
        "--ignore-axis", nargs="+", default=[], metavar="COLUMN",
        help="Manifest column(s) to leave out of block matching, for axes whose values are only valid "
             "alongside specific settings of these columns. Each option is then labelled with their values.")
    args = parser.parse_args()

    if args.axis in AXIS_ALIASES:
        resolved = AXIS_ALIASES[args.axis]
        print(f"'{args.axis}' is an alias for column '{resolved}'.")
        args.axis = resolved

    if args.axis not in AXIS_COLUMNS:
        sys.exit(f"Unknown axis '{args.axis}'. Choose one of: {', '.join(AXIS_COLUMNS)} (or an alias: {alias_help})")

    ignored = [AXIS_ALIASES.get(c, c) for c in args.ignore_axis]
    for column in ignored:
        if column not in AXIS_COLUMNS:
            sys.exit(f"Unknown --ignore-axis column '{column}'. Choose from: {', '.join(AXIS_COLUMNS)}")

        if column == args.axis:
            sys.exit(f"--ignore-axis can't include the tested axis '{args.axis}'.")

    df = load_manifest(args.folder)
    if args.axis not in df.columns:
        sys.exit(f"Column '{args.axis}' not present in the loaded manifest(s).")

    missing_ignored = [c for c in ignored if c not in df.columns]
    if missing_ignored:
        sys.exit(f"--ignore-axis column(s) not present in the loaded manifest(s): {', '.join(missing_ignored)}")

    if ignored:
        print(f"Ignoring {', '.join(ignored)} when matching blocks; options are labelled with their values.")

    if args.metric not in df.columns:
        sys.exit(f"Metric column '{args.metric}' not present in the loaded manifest(s).")

    if args.metric.startswith("phase_"):
        # Phases the run never enters (e.g. ballistics when it is off) are blank in the manifest
        df[args.metric] = df[args.metric].fillna(0)

    if "repeat_index" not in df.columns:
        print(
            "Note: no 'repeat_index' column found (manifest predates this being recorded) - "
            "repeats of the same block will be averaged together rather than treated as "
            "separate matched observations.",
        )

    level_col = add_level_column(df, args.axis, ignored)
    values = ordered_values(df, args.axis, level_col)
    if len(values) < 2:
        sys.exit(f"Only one '{args.axis}' option present ({values}) - nothing to compare.")

    blocks = build_blocks(df, args.axis, level_col, ignored, args.metric)
    if len(blocks) == 0:
        sys.exit("No complete blocks (a matched run for every axis value) - cannot run a paired test.")

    if len(values) == 2:
        run_two_value_test(blocks, args.axis, values)
    else:
        run_multi_value_test(blocks, args.axis, values, args.alpha)


if __name__ == "__main__":
    main()
