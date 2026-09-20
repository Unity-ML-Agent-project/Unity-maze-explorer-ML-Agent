"""
Export ML-Agents training curves to CSV, in the same format as TensorBoard's
"Download CSV" (Wall time,Step,Value), one file per run per metric:

    <out>/Cumulative Reward/<run>_MazeAgent.csv
    <out>/Entropy/<run>_MazeAgent.csv
    <out>/Episode Length/<run>_MazeAgent.csv

Also writes <out>/run_info.csv with per-run metadata (versions, observation
shape, training time) so the analysis can report the experiment environment.

Usage (needs the mlagents environment - it has tensorboard and onnx):
    python Tools/export_csv.py --results results results_server --out "C:/Users/user/Desktop/data"

Re-running is safe: existing CSVs for the same run are overwritten with identical data.
Folders starting with "_" (e.g. _trash_...) are skipped.
"""
import argparse
import csv
import datetime
import glob
import json
import os

from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

METRICS = {
    "Cumulative Reward": "Environment/Cumulative Reward",
    "Entropy": "Policy/Entropy",
    "Episode Length": "Environment/Episode Length",
}
BEHAVIOR = "MazeAgent"

parser = argparse.ArgumentParser()
parser.add_argument("--results", nargs="+", required=True, help="one or more ML-Agents results folders")
parser.add_argument("--out", required=True, help="data folder that holds the metric CSV folders")
args = parser.parse_args()


def observation_shapes(run_dir):
    try:
        import onnx
        model = onnx.load(os.path.join(run_dir, f"{BEHAVIOR}.onnx"))
        return " + ".join(
            "x".join(str(d.dim_value) for d in i.type.tensor_type.shape.dim[1:])
            for i in model.graph.input if i.name.startswith("obs_"))
    except Exception:
        return ""


def active_hours(wall_times):
    """Wall-clock training time, leaving out pauses between resumed segments
    (gaps longer than 10x the usual interval between summaries)."""
    gaps = [b - a for a, b in zip(wall_times, wall_times[1:])]
    if not gaps:
        return 0.0
    usual = sorted(gaps)[len(gaps) // 2]
    return sum(g for g in gaps if g <= 10 * usual) / 3600


info_rows = {}
info_path = os.path.join(args.out, "run_info.csv")
if os.path.exists(info_path):
    with open(info_path, newline="", encoding="utf-8") as f:
        info_rows = {r["run"]: r for r in csv.DictReader(f)}

exported = 0
for results_dir in args.results:
    for run_dir in sorted(glob.glob(os.path.join(results_dir, "*"))):
        run = os.path.basename(run_dir)
        events = glob.glob(os.path.join(run_dir, BEHAVIOR, "events.out.tfevents*"))
        if run.startswith("_") or not os.path.isdir(run_dir) or not events:
            continue
        # A run resumed with --resume leaves one event file per segment. Like TensorBoard,
        # concatenate all of them in time order (a step logged by both segments appears twice).
        scalars = {tag: [] for tag in METRICS.values()}
        for ev in events:
            acc = EventAccumulator(ev, size_guidance={"scalars": 0})
            acc.Reload()
            for tag in scalars:
                if tag in acc.Tags()["scalars"]:
                    scalars[tag] += acc.Scalars(tag)
        for tag in scalars:
            scalars[tag].sort(key=lambda e: e.wall_time)
        if len(events) > 1:
            print(f"[NOTE] {run}: {len(events)} event files (resumed run?) - merged in time order")

        for folder, tag in METRICS.items():
            if not scalars[tag]:
                print(f"[WARN] {run}: no '{tag}'")
                continue
            os.makedirs(os.path.join(args.out, folder), exist_ok=True)
            with open(os.path.join(args.out, folder, f"{run}_{BEHAVIOR}.csv"), "w", newline="") as f:
                w = csv.writer(f)
                w.writerow(["Wall time", "Step", "Value"])
                for e in scalars[tag]:
                    w.writerow([repr(e.wall_time), e.step, repr(float(e.value))])

        reward = scalars[METRICS["Cumulative Reward"]]
        meta = {}
        try:
            meta = json.load(open(os.path.join(run_dir, "run_logs", "timers.json"))).get("metadata", {})
        except Exception:
            pass
        config = ""
        try:
            config = open(os.path.join(run_dir, "configuration.yaml"), encoding="utf-8").read()
        except Exception:
            pass
        info_rows[run] = {
            "run": run,
            "results_dir": os.path.abspath(results_dir),
            "curriculum": "yes" if "MazeSize" in config else "no",
            "observation": observation_shapes(run_dir),
            "final_step": reward[-1].step,
            "start_time": datetime.datetime.fromtimestamp(reward[0].wall_time).isoformat(timespec="seconds"),
            "train_hours": round(active_hours([e.wall_time for e in reward]), 3),
            "segments": len(events),
            "mlagents_version": meta.get("mlagents_version", ""),
            "pytorch_version": meta.get("pytorch_version", ""),
            "python_version": meta.get("python_version", "").split(" ")[0],
        }
        exported += 1
        print(f"exported {run}  ({len(reward)} points, final step {reward[-1].step})")

with open(info_path, "w", newline="", encoding="utf-8") as f:
    fields = ["run", "results_dir", "curriculum", "observation", "final_step", "start_time",
              "train_hours", "segments", "mlagents_version", "pytorch_version", "python_version"]
    w = csv.DictWriter(f, fieldnames=fields)
    w.writeheader()
    for run in sorted(info_rows):
        w.writerow({k: info_rows[run].get(k, "") for k in fields})
print(f"\n{exported} runs exported to {os.path.abspath(args.out)}")
