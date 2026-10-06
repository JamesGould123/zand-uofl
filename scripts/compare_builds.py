"""Compare two separate benchmark session folders as a synthetic before/after axis -
e.g. one run with an optimization removed, one with it applied -
using the same matched-pairs Wilcoxon signed-rank approach as axis_significance_test.py,
but across two manifests instead of one column within a single manifest.

Usage:
    .venv/Scripts/python compare_builds.py <folder_a> <folder_b>
        [--metric COLUMN [COLUMN ...]] [--alpha 0.05]
        [--label-a NAME] [--label-b NAME] [--output-dir DIR]
"""

import argparse
import re
import sys
from datetime import datetime
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

DEFAULT_METRICS = ["avg_frame_ms", "avg_sim_step_ms", "phase_gridupdate_avg_ms", "phase_couplingbeforeca_avg_ms", "phase_render_avg_ms"]

TIMESTAMP_SUFFIX = re.compile(r"_\d{8}_\d{6}$")


def load_manifest(folder: Path) -> pd.DataFrame:
    path = folder / "manifest.csv"
    if not path.is_file():
        sys.exit(f"No manifest.csv in {folder}")

    return pd.read_csv(path, keep_default_na=False, na_values=[""])


def prefix_of(folder: Path) -> str:
    stripped = TIMESTAMP_SUFFIX.sub("", folder.name)
    return stripped if stripped else folder.name


def block_columns(a: pd.DataFrame, b: pd.DataFrame) -> list[str]:
    return [c for c in AXIS_COLUMNS if c in a.columns and c in b.columns]


def match_pairs(a: pd.DataFrame, b: pd.DataFrame, cols: list[str], metric: str) -> tuple[list[float], list[float], list[str]]:
    notes: list[str] = []
    a_vals: list[float] = []
    b_vals: list[float] = []
    has_repeat_index = "repeat_index" in a.columns and "repeat_index" in b.columns

    a_keys = set(map(tuple, a[cols].drop_duplicates().to_numpy())) if cols else {()}
    b_keys = set(map(tuple, b[cols].drop_duplicates().to_numpy())) if cols else {()}
    only_a = a_keys - b_keys
    only_b = b_keys - a_keys
    if only_a:
        notes.append(f"{len(only_a)} block(s) present only in run A, dropped: {sorted(only_a)}")
    if only_b:
        notes.append(f"{len(only_b)} block(s) present only in run B, dropped: {sorted(only_b)}")

    for key in sorted(a_keys & b_keys):
        a_group = a
        b_group = b
        for col, val in zip(cols, key):
            a_group = a_group[a_group[col] == val]
            b_group = b_group[b_group[col] == val]

        if has_repeat_index:
            common_repeats = sorted(set(a_group["repeat_index"]) & set(b_group["repeat_index"]))
            for r in common_repeats:
                a_vals.append(a_group.loc[a_group["repeat_index"] == r, metric].iloc[0])
                b_vals.append(b_group.loc[b_group["repeat_index"] == r, metric].iloc[0])

            if len(a_group) != len(common_repeats) or len(b_group) != len(common_repeats):
                notes.append(
                    f"Block {key}: matched {len(common_repeats)} repeat(s) by repeat_index "
                    f"(run A had {len(a_group)}, run B had {len(b_group)})",
                )
        else:
            n = min(len(a_group), len(b_group))
            a_vals.extend(a_group[metric].to_numpy()[:n])
            b_vals.extend(b_group[metric].to_numpy()[:n])
            if len(a_group) != len(b_group):
                notes.append(
                    f"Block {key}: no repeat_index column in one/both manifests, matched by "
                    f"position - used {n} of (run A {len(a_group)}, run B {len(b_group)}) repeat(s)",
                )

    return a_vals, b_vals, notes


def compare_metric(a: pd.DataFrame, b: pd.DataFrame, cols: list[str], metric: str, alpha: float) -> str:
    a_vals, b_vals, notes = match_pairs(a, b, cols, metric)
    lines = [f"### {metric}", ""]
    for note in notes:
        lines.append(f"- {note}")

    if len(a_vals) == 0:
        lines.append("\nNo matched pairs - nothing to compare.")
        return "\n".join(lines) + "\n"

    a_series = pd.Series(a_vals, dtype=float)
    b_series = pd.Series(b_vals, dtype=float)
    diff = b_series - a_series

    if (a_series == b_series).all():
        lines.append(f"\nn = {len(a_series)} matched pair(s), every pair identical - nothing to test (p = 1.0).")
        return "\n".join(lines) + "\n"

    result = stats.wilcoxon(a_series, b_series)
    verdict = "**SIGNIFICANT**" if result.pvalue < alpha else "not significant"
    pct = diff.mean() / a_series.mean() * 100 if a_series.mean() else 0.0
    lines.extend([
        "",
        f"n = {len(a_series)} matched pair(s)",
        f"Median: {a_series.median():.4f} -> {b_series.median():.4f}",
        f"Mean change (B - A): {diff.mean():+.4f} ({pct:+.2f}%)",
        f"Wilcoxon signed-rank: statistic={result.statistic:.3f}, p={result.pvalue:.5f} -> {verdict} (alpha={alpha})",
    ])
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folder_a", type=Path, help="First session folder (has manifest.csv) - e.g. the 'before' run")
    parser.add_argument("folder_b", type=Path, help="Second session folder - e.g. the 'after' run")
    parser.add_argument("--metric", nargs="+", default=DEFAULT_METRICS, help=f"Manifest column(s) to compare (default: {' '.join(DEFAULT_METRICS)})")
    parser.add_argument("--alpha", type=float, default=0.05, help="Significance threshold (default: 0.05)")
    parser.add_argument("--label-a", default=None, help="Short name for folder_a in the filename (default: its prefix)")
    parser.add_argument("--label-b", default=None, help="Short name for folder_b in the filename (default: its prefix)")
    parser.add_argument("--output-dir", type=Path, default=None, help="Where to write the report (default: folder_a's parent)")
    args = parser.parse_args()

    folder_a = args.folder_a.resolve()
    folder_b = args.folder_b.resolve()
    a = load_manifest(folder_a)
    b = load_manifest(folder_b)

    label_a = args.label_a or prefix_of(folder_a)
    label_b = args.label_b or prefix_of(folder_b)
    cols = block_columns(a, b)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_dir = args.output_dir or folder_a.parent
    output_dir.mkdir(parents=True, exist_ok=True)
    report_path = output_dir / f"compare_{label_a}_vs_{label_b}_{timestamp}.md"

    lines = [
        f"# Build comparison: {label_a} vs {label_b}",
        "",
        f"- Run A ({label_a}): `{folder_a}`",
        f"- Run B ({label_b}): `{folder_b}`",
        f"- Blocked on: {', '.join(cols) if cols else '(no shared config columns - treating every row as one block)'}",
        "",
    ]
    for metric in args.metric:
        if metric not in a.columns or metric not in b.columns:
            lines.append(f"### {metric}\n\nColumn not present in both manifests - skipped.\n")
            continue

        lines.append(compare_metric(a, b, cols, metric, args.alpha))

    report = "\n".join(lines)
    report_path.write_text(report, encoding="utf-8")
    print(report)
    print(f"\nSaved to {report_path}")


if __name__ == "__main__":
    main()
