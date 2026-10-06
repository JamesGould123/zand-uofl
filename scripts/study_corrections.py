"""Applies study-wide multiple-comparison corrections to a finished testing plan and build comparison
batch, and adds effect-size checks for the build comparisons.

Usage:
    .venv/Scripts/python study_corrections.py <rerun-batch folder> --summaries SUMMARY [SUMMARY ...]
        [--output-dir DIR] [--alpha 0.05] [--bootstrap 10000]
"""

import argparse
import contextlib
import io
import itertools
import re
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from scipy import stats

import axis_significance_test as axis_test
import run_testing_plan as plan_runner

PRIMARY_METRIC = "avg_frame_ms"
NOISE_CHECK = "noise-check"
FOLDER_PATTERN = re.compile(r"`([^`]+)`")
COMPARISON_HEADER = re.compile(r"^## (\S+): (\S+) \(A\) vs (\S+) \(B\)$")
COMBINED_LINE = re.compile(r"^- combined: `([^`]+)`, `([^`]+)`$")


def holm(p_values: np.ndarray) -> np.ndarray:
    """Holm step-down adjusted p-values, in the original order."""
    m = len(p_values)
    order = np.argsort(p_values)
    adjusted = np.empty(m)
    running_max = 0.0
    for rank, i in enumerate(order):
        running_max = max(running_max, min((m - rank) * p_values[i], 1.0))
        adjusted[i] = running_max

    return adjusted


def wilcoxon_p(x: np.ndarray, y: np.ndarray) -> float:
    """Two-sided Wilcoxon signed-rank p-value; 1.0 when every pair is identical (nothing to test)."""
    if np.array_equal(x, y):
        return 1.0

    return float(stats.wilcoxon(x, y).pvalue)


def axis_rows(summary_path: Path, plan: dict) -> list[tuple[dict, list[Path]]]:
    """(plan entry, session folders) for every axis row of the testing plan summary table."""
    entries = {entry["axis"]: entry for entry in plan["axes"]}
    rows = []
    for line in summary_path.read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 4 or cells[0] not in entries:
            continue

        folders = [Path(f) for f in FOLDER_PATTERN.findall(cells[3])]
        rows.append((entries[cells[0]], folders))

    return rows


def summary_kind(summary_path: Path) -> str:
    """The source label for a testing plan summary"""
    if summary_path.name.startswith("testing-plan-heavy-"):
        return "testing plan (heavy)"

    if summary_path.name.startswith("testing-plan-light-"):
        return "testing plan (light parallelism)"

    if summary_path.name.startswith("testing-plan-bodies-"):
        return "testing plan (body stress)"

    return "testing plan"


def axis_tests(entry: dict, folders: list[Path], source: str) -> list[dict]:
    axis = entry["manifest_column"]
    ignored = entry.get("ignore_columns", [])
    with contextlib.redirect_stdout(io.StringIO()):
        base = axis_test.load_manifest(folders)

    tests = []
    for metric in plan_runner.metrics_for(entry):
        if metric not in base.columns:
            continue

        df = base.copy()
        if metric.startswith("phase_"):
            df[metric] = df[metric].fillna(0)

        level_col = axis_test.add_level_column(df, axis, ignored)
        values = axis_test.ordered_values(df, axis, level_col)
        with contextlib.redirect_stdout(io.StringIO()):
            blocks = axis_test.build_blocks(df, axis, level_col, ignored, metric)

        pairs = list(itertools.combinations(values, 2))
        raw = np.array([wilcoxon_p(blocks[a].to_numpy(), blocks[b].to_numpy()) for a, b in pairs])
        within_axis = holm(raw) if len(pairs) > 1 else raw
        friedman_p = None
        if len(values) > 2:
            friedman_p = float(stats.friedmanchisquare(*[blocks[v].to_numpy() for v in values]).pvalue)

        for (a, b), p, p_axis in zip(pairs, raw, within_axis):
            tests.append({
                "source": source, "axis": entry["axis"], "metric": metric,
                "comparison": f"{a} vs {b}", "n": len(blocks),
                "median_a": blocks[a].median(), "median_b": blocks[b].median(),
                "p_raw": p, "p_within_axis": p_axis, "friedman_p": friedman_p,
            })

    return tests


def build_comparisons(batch_dir: Path) -> list[tuple[str, str, str, Path, Path]]:
    runs_dir = batch_dir.parent
    found = []
    current = None
    for line in (batch_dir / "summary.md").read_text(encoding="utf-8").splitlines():
        header = COMPARISON_HEADER.match(line)
        if header:
            current = header.groups()
            continue

        combined = COMBINED_LINE.match(line)
        if combined and current:
            found.append((*current, runs_dir / combined.group(1), runs_dir / combined.group(2)))
            current = None

    return found


def hodges_lehmann_ci(diffs: np.ndarray, resamples: int, rng: np.random.Generator) -> tuple[float, float, float]:
    """Hodges-Lehmann estimate (median of all pairwise averages of the differences)
    and a bootstrap percentile 95% confidence interval for it.
    """
    def estimate(d: np.ndarray) -> float:
        i, j = np.triu_indices(len(d))
        return float(np.median((d[i] + d[j]) / 2))

    boot = [estimate(rng.choice(diffs, size=len(diffs), replace=True)) for _ in range(resamples)]
    return estimate(diffs), float(np.percentile(boot, 2.5)), float(np.percentile(boot, 97.5))


def build_tests(comparisons, resamples: int, rng: np.random.Generator) -> tuple[list[dict], list[dict]]:
    """Paired launch tests for every build comparison and metric, plus the effect-size checks."""
    tests = []
    checks = []
    for comparison, label_a, label_b, folder_a, folder_b in comparisons:
        a = pd.read_csv(folder_a / "manifest.csv").set_index("repeat_index")
        b = pd.read_csv(folder_b / "manifest.csv").set_index("repeat_index")
        shared = a.index.intersection(b.index)
        for metric in [m for m in a.columns if m.startswith(("avg_", "phase_")) and m in b.columns]:
            if metric.startswith("avg_memory") or metric.startswith("avg_cpu"):
                continue

            x = a.loc[shared, metric].to_numpy()
            y = b.loc[shared, metric].to_numpy()
            p = wilcoxon_p(x, y)
            tests.append({
                "source": "build comparison", "axis": comparison, "metric": metric,
                "comparison": f"{label_a} vs {label_b}", "n": len(shared),
                "median_a": float(np.median(x)), "median_b": float(np.median(y)),
                "p_raw": p, "p_within_axis": p, "friedman_p": None,
            })
            if metric not in (PRIMARY_METRIC, "avg_sim_step_ms"):
                continue

            diffs = y - x
            estimate, low, high = hodges_lehmann_ci(diffs, resamples, rng)
            mean_a = float(np.mean(x))
            half = len(shared) // 2
            halves = []
            for part in (slice(0, half), slice(half, None)):
                hx, hy = x[part], y[part]
                halves.append((100 * float(np.mean(hy - hx)) / float(np.mean(hx)), wilcoxon_p(hx, hy)))

            checks.append({
                "comparison": comparison, "metric": metric, "n": len(shared),
                "hl_ms": estimate, "ci_low_ms": low, "ci_high_ms": high,
                "hl_pct": 100 * estimate / mean_a, "ci_low_pct": 100 * low / mean_a, "ci_high_pct": 100 * high / mean_a,
                "b_faster": int(np.sum(diffs < 0)), "b_slower": int(np.sum(diffs > 0)), "ties": int(np.sum(diffs == 0)),
                "half1_pct": halves[0][0], "half1_p": halves[0][1], "half2_pct": halves[1][0], "half2_p": halves[1][1],
            })

    return tests, checks


def verdict(p: float, alpha: float) -> str:
    return "sig" if p < alpha else "ns"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("batch", type=Path, help="rerun-batch_<timestamp> folder")
    parser.add_argument("--summaries", type=Path, nargs="+", required=True,
                        help="Testing plan summaries, in order; later ones replace the same axis from earlier ones")
    parser.add_argument("--output-dir", type=Path, default=None, help="Where to write the results (default: the batch folder)")
    parser.add_argument("--alpha", type=float, default=0.05)
    parser.add_argument("--bootstrap", type=int, default=10000, help="Bootstrap resamples for the confidence intervals")
    args = parser.parse_args()

    # The plan the batch actually ran with, from its scripts snapshot when available.
    snapshot_plan = args.batch / plan_runner.SCRIPTS_SNAPSHOT_DIR_NAME / "axis_test_plan.json"
    if snapshot_plan.is_file():
        plan_runner.AXIS_TEST_PLAN = snapshot_plan
    plan = plan_runner.load_plan()

    rows = []
    selected = {}
    for summary in args.summaries:
        source = summary_kind(summary)
        for entry, folders in axis_rows(summary, plan):
            selected[(source, entry["axis"])] = (entry, folders, summary.name)

    for (source, axis), (entry, folders, summary_name) in selected.items():
        print(f"{source}: {axis} from {summary_name} ({len(folders)} session folder(s))...", flush=True)
        rows += axis_tests(entry, folders, source)

    rng = np.random.default_rng(12345)
    comparisons = build_comparisons(args.batch)
    print(f"Build comparisons: {', '.join(c[0] for c in comparisons)}", flush=True)
    build_rows, checks = build_tests(comparisons, args.bootstrap, rng)
    rows += build_rows

    table = pd.DataFrame(rows)
    table["family"] = np.where(table.metric == PRIMARY_METRIC, "primary", "secondary")
    table.loc[(table.source == "build comparison") & (table.axis == NOISE_CHECK), "family"] = "noise check"
    table["p_bh"] = np.nan
    table["p_holm"] = np.nan
    for family in ("primary", "secondary"):
        mask = table.family == family
        p = table.loc[mask, "p_raw"].to_numpy()
        table.loc[mask, "p_bh"] = stats.false_discovery_control(p, method="bh")
        table.loc[mask, "p_holm"] = holm(p)

    output_dir = args.output_dir or args.batch
    output_dir.mkdir(parents=True, exist_ok=True)
    table.to_csv(output_dir / "study-corrections.csv", index=False)

    a = args.alpha
    lines = [
        "# Study-wide multiple-comparison corrections",
        "",
        *[f"- Testing plan summary (in order, later ones replace the same axis): `{summary}`" for summary in args.summaries],
        f"- Build comparisons: `{args.batch}`",
        f"- alpha = {a}; Benjamini-Hochberg is the main correction, Holm is shown for comparison.",
        "- Raw pairwise p-values are used for multi-value axes (no Friedman gate).",
        "",
        "## Overview",
        "",
        "| Family | Tests | Significant (raw) | Significant after BH | Significant after Holm |",
        "|---|---|---|---|---|",
    ]
    for family in ("primary", "secondary"):
        part = table[table.family == family]
        lines.append(f"| {family} | {len(part)} | {(part.p_raw < a).sum()} | {(part.p_bh < a).sum()} | {(part.p_holm < a).sum()} |")

    def fmt_p(p: float) -> str:
        return "<0.00001" if p < 0.00001 else f"{p:.5f}"

    for family in ("primary", "secondary"):
        part = table[table.family == family]
        lost = part[(part.p_raw < a) & (part.p_bh >= a)]
        lost_holm = part[(part.p_bh < a) & (part.p_holm >= a)]
        lines += ["", f"## {family.capitalize()} family: results that are no longer significant", ""]
        if lost.empty:
            lines.append("None: every result significant before correction is still significant after BH.")
        else:
            lines += ["Significant before correction, not after BH:", "",
                      "| Source | Axis | Metric | Comparison | n | Medians (A / B) | Raw p | Within-axis Holm p | BH p | Holm p |",
                      "|---|---|---|---|---|---|---|---|---|---|"]
            for r in lost.sort_values("p_raw").itertuples():
                lines.append(f"| {r.source} | {r.axis} | {r.metric} | {r.comparison} | {r.n} | {r.median_a:.4f} / {r.median_b:.4f} | "
                             f"{fmt_p(r.p_raw)} | {fmt_p(r.p_within_axis)} | {fmt_p(r.p_bh)} | {fmt_p(r.p_holm)} |")

        if not lost_holm.empty:
            lines += ["", "Still significant after BH, but not after the stricter Holm correction:", "",
                      "| Source | Axis | Metric | Comparison | BH p | Holm p |", "|---|---|---|---|---|---|"]
            for r in lost_holm.sort_values("p_raw").itertuples():
                lines.append(f"| {r.source} | {r.axis} | {r.metric} | {r.comparison} | {fmt_p(r.p_bh)} | {fmt_p(r.p_holm)} |")

    primary = table[table.family == "primary"].sort_values(["source", "axis", "p_raw"], ascending=[False, True, True])
    lines += ["", "## Every primary (frametime) test", "",
              "| Source | Axis | Comparison | n | Medians (A / B) | Raw p | Within-axis Holm p | BH p | Holm p | Verdict (BH) |",
              "|---|---|---|---|---|---|---|---|---|---|"]
    for r in primary.itertuples():
        lines.append(f"| {r.source} | {r.axis} | {r.comparison} | {r.n} | {r.median_a:.4f} / {r.median_b:.4f} | {fmt_p(r.p_raw)} | "
                     f"{fmt_p(r.p_within_axis)} | {fmt_p(r.p_bh)} | {fmt_p(r.p_holm)} | {verdict(r.p_bh, a)} |")

    noise = table[table.family == "noise check"]
    lines += ["", "## Noise check (not part of either family)", "",
              "| Metric | n | Medians (A / B) | Raw p |", "|---|---|---|---|"]
    for r in noise.itertuples():
        lines.append(f"| {r.metric} | {r.n} | {r.median_a:.4f} / {r.median_b:.4f} | {fmt_p(r.p_raw)} |")

    lines += ["", "## Build comparisons: effect size and consistency", "",
              "Differences are B - A (negative = B faster), per launch pair. The Hodges-Lehmann estimate is the "
              f"median of all pairwise averages of the differences; its 95% interval is a bootstrap percentile interval "
              f"({args.bootstrap} resamples). Percentages are relative to side A's mean. Each half is the first or last "
              "half of the launch pairs, tested on its own.", "",
              "| Comparison | Metric | n | Estimate | 95% interval | B faster / slower / tied | First half | Second half |",
              "|---|---|---|---|---|---|---|---|"]
    for c in checks:
        lines.append(
            f"| {c['comparison']} | {c['metric']} | {c['n']} | {c['hl_ms']:+.4f} ms ({c['hl_pct']:+.2f}%) | "
            f"{c['ci_low_pct']:+.2f}% to {c['ci_high_pct']:+.2f}% | {c['b_faster']} / {c['b_slower']} / {c['ties']} | "
            f"{c['half1_pct']:+.2f}%, p = {fmt_p(c['half1_p'])} | {c['half2_pct']:+.2f}%, p = {fmt_p(c['half2_p'])} |")

    report = "\n".join(lines) + "\n"
    (output_dir / "study-corrections.md").write_text(report, encoding="utf-8")
    print(f"\nSaved {output_dir / 'study-corrections.md'} and study-corrections.csv")


if __name__ == "__main__":
    main()
