"""
Analysis of the maze sensor experiments (training curves exported by export_csv.py).

Reads   <data>/Cumulative Reward|Entropy|Episode Length/<run>_MazeAgent.csv  (+ run_info.csv)
Writes  <data>/Analysis/
          summary.md                  report-ready tables + definitions + warnings
          summary_by_group.csv        one row per sensor x curriculum group
          summary_by_run.csv          one row per training run
          comparison_<metric>.png     all sensors, no-curriculum vs curriculum
          by_sensor_<metric>.png      per sensor: curriculum vs no-curriculum
          final_reward.png            final reward of every run
          per_group/<metric>/<group>/ Average_*.csv, StdDev_*.csv, curve png
                                      (same layout as the original "process" script)

Usage (any Python with pandas + matplotlib):
    python Tools/analyze.py --data "C:/Users/user/Desktop/data"

To add a sensor, add one line to SENSORS. Run names are matched case-sensitively
against the full pattern, so e.g. "Grid_001" (old, invalid) never matches "grid_\\d+".
"""
import argparse
import csv
import os
import re

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

METRICS = ["Cumulative Reward", "Entropy", "Episode Length"]

# label, colour, no-curriculum pattern, curriculum pattern, no-curriculum folder, curriculum folder
SENSORS = [
    ("Vector",        "#2a78d6", r"vector_\d+",        r"vector_curriculum_\d+",        "vector",        "vector_curriculum"),
    ("Raycast",       "#eb6834", r"raycast_\d+",       r"raycast_curriculum_\d+",       "raycast",       "raycast_curriculum"),
    ("Grid",          "#1baf7a", r"grid_\d+",          r"grid_curriculum_\d+",          "grid",          "grid_curriculum"),
    ("Camera",        "#eda100", r"camera_\d+",        r"camera_curriculum_\d+",        "camera",        "camera_curriculum"),
    ("Hybrid AbsDis", "#e87ba4", r"Hybrid_AbsDis_v\d+", r"Hybrid_AbsDis_Curriculum_v\d+", "Hybrid_AbsDis", "Hybrid_AbsDis_Curriculum"),
    ("Hybrid RelDis", "#008300", r"Hybrid_RelDis_v\d+", r"Hybrid_RelDis_Curriculum_v\d+", "Hybrid_RelDis", "Hybrid_RelDis_Curriculum"),
]
MIN_RUNS = 5           # warn below this; group sizes may differ between sensors
FINAL_WINDOW = 100_000   # "final" = mean over the last 100k steps
SUCCESS_SPAN = 200_000   # paper: reward above zero for over 200k steps before the end of training

INK, INK2, GRID, AXIS, SURFACE = "#0b0b0b", "#52514e", "#e6e5e1", "#c9c8c3", "#fcfcfb"
plt.rcParams.update({
    "figure.facecolor": SURFACE, "axes.facecolor": SURFACE, "savefig.facecolor": SURFACE,
    "axes.edgecolor": AXIS, "axes.labelcolor": INK2, "axes.titlecolor": INK,
    "axes.spines.top": False, "axes.spines.right": False,
    "axes.grid": True, "grid.color": GRID, "grid.linewidth": 0.6,
    "xtick.color": INK2, "ytick.color": INK2, "text.color": INK,
    "font.size": 10, "axes.titlesize": 11, "axes.titleweight": "bold",
    "legend.frameon": False, "lines.linewidth": 1.6,
})


def step_fmt(x, _=None):
    return f"{x / 1e6:.1f}M" if x >= 1e6 or x == 0 else f"{x / 1e3:.0f}k"


def load(data_dir):
    """runs[metric][run] -> DataFrame(Step, Value, Wall time)"""
    runs = {m: {} for m in METRICS}
    for m in METRICS:
        folder = os.path.join(data_dir, m)
        if not os.path.isdir(folder):
            continue
        for name in sorted(os.listdir(folder)):
            match = re.fullmatch(r"(.+)_MazeAgent\.csv", name)
            if match:
                df = pd.read_csv(os.path.join(folder, name))
                # resumed runs log the resume step twice: keep the later (resumed) value
                df = df.sort_values("Wall time").drop_duplicates("Step", keep="last").sort_values("Step")
                runs[m][match.group(1)] = df.reset_index(drop=True)
    return runs


def groups_for(run_names):
    """-> list of (sensor_label, colour, curriculum_bool, folder, [runs])"""
    out = []
    for label, colour, p_no, p_cl, f_no, f_cl in SENSORS:
        for cl, pat, folder in ((False, p_no, f_no), (True, p_cl, f_cl)):
            members = sorted(r for r in run_names if re.fullmatch(pat, r))
            out.append((label, colour, cl, folder, members))
    return out


def run_stats(reward, entropy, length):
    s, v = reward["Step"].to_numpy(), reward["Value"].to_numpy()
    last = s[-1]
    interval = int(np.median(np.diff(s))) if len(s) > 1 else 10_000
    final = v[s > last - FINAL_WINDOW].mean()
    k = 0
    while k < len(v) and v[-1 - k] > 0:
        k += 1
    positive_span = k * interval
    first_pos = s[np.argmax(v > 0)] if (v > 0).any() else np.nan
    tail = lambda df: df["Value"][df["Step"] > df["Step"].iloc[-1] - FINAL_WINDOW].mean() if df is not None else np.nan
    return {
        "final_step": int(last), "points": len(s),
        "final_reward": final, "max_reward": v.max(),
        "trailing_positive_steps": positive_span,
        "training_success": positive_span > SUCCESS_SPAN,
        "first_positive_step": first_pos,
        "final_entropy": tail(entropy), "final_episode_length": tail(length),
        "train_hours": active_hours(reward["Wall time"].sort_values().to_numpy()),
    }


def active_hours(wall):
    """Wall-clock time between the first and last summary, leaving out pauses
    between resumed segments (gaps longer than 10x the usual interval)."""
    gaps = np.diff(wall)
    if len(gaps) == 0:
        return 0.0
    return gaps[gaps <= 10 * np.median(gaps)].sum() / 3600


def mean_std(frames):
    wide = pd.concat([f.set_index("Step")["Value"] for f in frames], axis=1).sort_index()
    return wide.mean(axis=1), wide.std(axis=1)


def label_axes(ax, metric):
    ax.xaxis.set_major_formatter(step_fmt)
    ax.set_xlabel("Training steps")
    ax.set_ylabel(metric)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True, help="folder holding the metric CSV folders")
    ap.add_argument("--out", default=None, help="output folder (default <data>/Analysis)")
    args = ap.parse_args()
    out = args.out or os.path.join(args.data, "Analysis")
    os.makedirs(out, exist_ok=True)

    runs = load(args.data)
    reward_runs = runs["Cumulative Reward"]
    info = {}
    info_path = os.path.join(args.data, "run_info.csv")
    if os.path.exists(info_path):
        with open(info_path, newline="", encoding="utf-8") as f:
            info = {r["run"]: r for r in csv.DictReader(f)}

    groups = groups_for(reward_runs.keys())
    warnings = []
    matched = {r for g in groups for r in g[4]}
    unmatched = sorted(set(reward_runs) - matched)
    if unmatched:
        warnings.append(f"這些訓練沒有對應到任何組別，未納入分析：{', '.join(unmatched)}")

    # ---------- per-run and per-group statistics ----------
    run_rows, group_rows = [], []
    for label, colour, cl, folder, members in groups:
        if not members:
            continue
        if len(members) < MIN_RUNS:
            warnings.append(f"{folder}：只有 {len(members)} 次訓練，樣本數偏少")
        other = next((g for g in groups if g[0] == label and g[2] != cl and g[4]), None)
        if other and len(other[4]) != len(members):
            warnings.append(f"{label}：有無課程學習的訓練次數不同（{len(members)} vs {len(other[4])}），"
                            "比較兩者時請留意樣本數差異")
        stats = []
        for r in members:
            st = run_stats(reward_runs[r], runs["Entropy"].get(r), runs["Episode Length"].get(r))
            st.update({"run": r, "group": folder, "sensor": label, "curriculum": cl,
                       "observation": info.get(r, {}).get("observation", ""),
                       "mlagents_version": info.get(r, {}).get("mlagents_version", ""),
                       "pytorch_version": info.get(r, {}).get("pytorch_version", "")})
            if st["final_step"] < 1_190_000:
                warnings.append(f"{r}：只跑到 {st['final_step']:,} 步，可能沒跑完")
            if st["points"] < 115:
                warnings.append(f"{r}：只有 {st['points']} 筆紀錄（正常約 120 筆），曲線可能有缺漏")
            if int(info.get(r, {}).get("segments") or 1) > 1:
                warnings.append(f"{r}：訓練中斷後接續（resume）過，分成 {info[r]['segments']} 段；"
                                "訓練時間已扣除中斷的空檔")
            stats.append(st)
        run_rows += stats
        df = pd.DataFrame(stats)
        group_rows.append({
            "group": folder, "sensor": label, "curriculum": "yes" if cl else "no", "runs": len(df),
            "final_reward_mean": df.final_reward.mean(), "final_reward_std": df.final_reward.std(),
            "final_reward_min": df.final_reward.min(), "final_reward_max": df.final_reward.max(),
            "success_runs": int(df.training_success.sum()),
            "success_rate": df.training_success.mean(),
            "first_positive_step_median": df.first_positive_step.median(),
            "final_entropy_mean": df.final_entropy.mean(),
            "final_episode_length_mean": df.final_episode_length.mean(),
            "train_hours_mean": df.train_hours.mean(), "train_hours_std": df.train_hours.std(),
            "observation": " / ".join(sorted(set(df.observation) - {""})),
            "mlagents_version": " / ".join(sorted(set(df.mlagents_version) - {""})),
            "pytorch_version": " / ".join(sorted(set(df.pytorch_version) - {""})),
        })
        if df.train_hours.max() > 1.3 * df.train_hours.min():
            warnings.append(f"{folder}：各次訓練時間差異大（{df.train_hours.min():.2f}–{df.train_hours.max():.2f} 小時），"
                            "比較訓練時間前請確認執行條件是否一致")

    run_df = pd.DataFrame(run_rows)
    group_df = pd.DataFrame(group_rows)
    run_df.to_csv(os.path.join(out, "summary_by_run.csv"), index=False, float_format="%.4f")
    group_df.to_csv(os.path.join(out, "summary_by_group.csv"), index=False, float_format="%.4f")

    # ---------- per-group curves (same layout as the original script) ----------
    for m in METRICS:
        for label, colour, cl, folder, members in groups:
            frames = [runs[m][r] for r in members if r in runs[m]]
            if not frames:
                continue
            d = os.path.join(out, "per_group", m, folder)
            os.makedirs(d, exist_ok=True)
            avg, std = mean_std(frames)
            avg.rename("Average_Value").reset_index().to_csv(os.path.join(d, f"Average_{folder}.csv"), index=False)
            std.rename("Std_Value").reset_index().to_csv(os.path.join(d, f"StdDev_{folder}.csv"), index=False)
            fig, ax = plt.subplots(figsize=(8, 4.5))
            ax.fill_between(avg.index, avg - std, avg + std, color=colour, alpha=0.18, lw=0, label="±1 std")
            ax.plot(avg.index, avg, color=colour, label=f"Mean of {len(frames)} runs")
            ax.set_title(f"{m}: {label}{' (curriculum)' if cl else ''}", loc="left")
            label_axes(ax, m)
            ax.legend(loc="best")
            fig.tight_layout()
            fig.savefig(os.path.join(d, f"{m.replace(' ', '_')}_Curve_{folder}.png"), dpi=150)
            plt.close(fig)

    # ---------- comparison: all sensors, one panel per curriculum condition ----------
    for m in METRICS:
        fig, axes = plt.subplots(1, 2, figsize=(12, 4.6), sharey=True)
        handles = {}
        for ax, cl in zip(axes, (False, True)):
            for label, colour, gcl, folder, members in groups:
                frames = [runs[m][r] for r in members if r in runs[m]]
                if gcl != cl or not frames:
                    continue
                avg, _ = mean_std(frames)
                (h,) = ax.plot(avg.index, avg, color=colour, label=label)
                handles[label] = h
            ax.set_title("Curriculum" if cl else "No curriculum", loc="left")
            label_axes(ax, m)
        axes[1].set_ylabel("")
        fig.legend(handles.values(), handles.keys(), loc="upper center", ncol=len(handles), bbox_to_anchor=(0.5, 1.0))
        fig.suptitle(f"{m} - mean of runs per sensor", x=0.01, y=0.93, ha="left", fontweight="bold", fontsize=12)
        fig.tight_layout(rect=(0, 0, 1, 0.88))
        fig.savefig(os.path.join(out, f"comparison_{m.replace(' ', '_')}.png"), dpi=150)
        plt.close(fig)

    # ---------- per sensor: curriculum vs no curriculum ----------
    present = [s for s in SENSORS if any(g[0] == s[0] and g[4] for g in groups)]
    for m in METRICS:
        cols = 3
        rows = max(1, int(np.ceil(len(present) / cols)))
        fig, axes = plt.subplots(rows, cols, figsize=(13, 3.6 * rows), sharex=True, sharey=True, squeeze=False)
        for ax in axes.flat[len(present):]:
            ax.set_visible(False)
        for ax, (label, colour, *_rest) in zip(axes.flat, present):
            for _l, _c, cl, folder, members in [g for g in groups if g[0] == label]:
                frames = [runs[m][r] for r in members if r in runs[m]]
                if not frames:
                    continue
                avg, std = mean_std(frames)
                ax.fill_between(avg.index, avg - std, avg + std, color=colour, alpha=0.12 if cl else 0.08, lw=0)
                ax.plot(avg.index, avg, color=colour, ls="-" if cl else "--",
                        label=f"Curriculum (n={len(frames)})" if cl else f"No curriculum (n={len(frames)})")
            ax.set_title(label, loc="left")
            ax.xaxis.set_major_formatter(step_fmt)
            ax.legend(loc="best", fontsize=8.5)
        for ax in axes[-1]:
            ax.set_xlabel("Training steps")
        for ax in axes[:, 0]:
            ax.set_ylabel(m)
        fig.suptitle(f"{m} - curriculum (solid) vs no curriculum (dashed), mean ±1 std",
                     x=0.01, ha="left", fontweight="bold", fontsize=12)
        fig.tight_layout()
        fig.savefig(os.path.join(out, f"by_sensor_{m.replace(' ', '_')}.png"), dpi=150)
        plt.close(fig)

    # ---------- final reward of every run ----------
    fig, ax = plt.subplots(figsize=(11, 4.6))
    rng = np.random.default_rng(0)
    ticks = []
    for i, (label, colour, *_rest) in enumerate(present):
        ticks.append(i)
        for cl, dx, marker in ((False, -0.17, "o"), (True, 0.17, "s")):
            sub = run_df[(run_df.sensor == label) & (run_df.curriculum == cl)]
            if sub.empty:
                continue
            x = i + dx + rng.uniform(-0.05, 0.05, len(sub))
            ax.scatter(x, sub.final_reward, s=42, marker=marker, color=colour if cl else SURFACE,
                       edgecolor=colour, linewidth=1.6, zorder=3)
            mu = sub.final_reward.mean()
            ax.hlines(mu, i + dx - 0.12, i + dx + 0.12, color=INK, lw=2, zorder=4)
    ax.set_xticks(ticks, [s[0] for s in present])
    ax.set_ylabel(f"Mean reward, last {FINAL_WINDOW // 1000}k steps")
    ax.set_title("Final reward of every run (bar = group mean)", loc="left")
    ax.grid(axis="x", visible=False)
    ax.scatter([], [], marker="o", color=SURFACE, edgecolor=INK2, linewidth=1.6, label="No curriculum")
    ax.scatter([], [], marker="s", color=INK2, label="Curriculum")
    ax.legend(loc="upper left", ncol=2)
    fig.tight_layout()
    fig.savefig(os.path.join(out, "final_reward.png"), dpi=150)
    plt.close(fig)

    write_markdown(out, group_df, warnings)
    print(f"Done. {len(run_df)} runs in {len(group_df)} groups -> {os.path.abspath(out)}")
    for w in warnings:
        print("  [!]", w)


def write_markdown(out, g, warnings):
    fmt = lambda x, p=3: "—" if pd.isna(x) else f"{x:.{p}f}"
    lines = [
        "# 迷宮感測器實驗：訓練結果總表", "",
        "由 `Tools/analyze.py` 自動產生，重新執行即可更新。", "",
        "## 各組結果", "",
        "| 感測器 | 課程學習 | 次數 | 最終獎勵 平均 ± 標準差 | 最低 – 最高 | 訓練成功 | 首次得分步數（中位數） | 最終 Entropy | 最終回合長度 | 訓練時間（小時） |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for _, r in g.iterrows():
        lines.append(
            f"| {r.sensor} | {'有' if r.curriculum == 'yes' else '無'} | {r.runs} | "
            f"{fmt(r.final_reward_mean)} ± {fmt(r.final_reward_std)} | {fmt(r.final_reward_min)} – {fmt(r.final_reward_max)} | "
            f"{r.success_runs}/{r.runs}（{r.success_rate:.0%}） | "
            f"{'—' if pd.isna(r.first_positive_step_median) else f'{r.first_positive_step_median:,.0f}'} | "
            f"{fmt(r.final_entropy_mean)} | {fmt(r.final_episode_length_mean, 0)} | "
            f"{fmt(r.train_hours_mean, 2)} ± {fmt(r.train_hours_std, 2)} |")
    lines += [
        "", "## 實驗環境（各組實際使用的版本）", "",
        "| 感測器 | 課程學習 | 觀察維度 | mlagents | PyTorch |", "|---|---|---|---|---|",
    ]
    for _, r in g.iterrows():
        lines.append(f"| {r.sensor} | {'有' if r.curriculum == 'yes' else '無'} | {r.observation or '—'} | "
                     f"{r.mlagents_version or '—'} | {r.pytorch_version or '—'} |")
    lines += [
        "", "## 指標定義", "",
        f"- **最終獎勵**：每次訓練最後 {FINAL_WINDOW:,} 步的平均累積獎勵（TensorBoard 每 10,000 步一筆，取最後幾筆平均）。",
        f"- **訓練成功**：依原論文定義，訓練結束前累積獎勵連續大於 0 超過 {SUCCESS_SPAN:,} 步。",
        "- **首次得分步數**：累積獎勵第一次大於 0 的步數，代表多快開始學到東西。",
        "- **最終 Entropy / 回合長度**：最後 100,000 步的平均。Entropy 越低代表策略越確定；回合越短代表越快走到終點。",
        "- **訓練時間**：TensorBoard 第一筆到最後一筆紀錄的實際時間，不含啟動時間，也扣除中斷後接續（resume）或電腦暫停的空檔。受硬體、同時執行的訓練數量影響，只適合比較條件相同的組別。",
    ]
    if warnings:
        lines += ["", "## 需要注意", ""] + [f"- {w}" for w in warnings]
    with open(os.path.join(out, "summary.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
