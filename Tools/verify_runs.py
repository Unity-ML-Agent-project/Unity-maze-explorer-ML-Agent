"""
Check finished ML-Agents runs before trusting their numbers.

For every run folder it reports the model's observation shape, whether the
training config matches the group standard, the wall-clock training time, how
many summary points the TensorBoard log holds, the final reward, and any
checkpoint files left behind by an earlier run in the same folder.

Usage (needs the mlagents environment - it has tensorboard and onnx):
    python Tools/verify_runs.py results_new
    python Tools/verify_runs.py ~/maze/results          # on the server

A run is fine when: the observation shape is what that sensor should produce,
cfg is "ok", it has ~120 points, and the stale column is "-".
"""
import argparse
import datetime
import glob
import json
import os
import re

import onnx
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

BEHAVIOR = "MazeAgent"
REWARD_TAG = "Environment/Cumulative Reward"
# the original author's settings - every group trains with these
STANDARD = {"max_steps": "1200000", "time_horizon": "64", "summary_freq": "10000",
            "batch_size": "100", "buffer_size": "1000", "learning_rate": "1.0e-05",
            "beta": "0.05", "epsilon": "0.1", "lambd": "0.95", "num_epoch": "2",
            "hidden_units": "128", "num_layers": "2", "gamma": "0.99"}

parser = argparse.ArgumentParser()
parser.add_argument("results", help="folder holding the run folders")
parser.add_argument("--expected-steps", type=int, default=1_200_000)
args = parser.parse_args()

print(f"{'run':24s} {'observation':16s} {'cfg':4s} {'start':12s} {'hours':>5s} {'pts':>4s} {'final':>7s}  stale")
problems = 0
for run_dir in sorted(glob.glob(os.path.join(args.results, "*"))):
    run = os.path.basename(run_dir)
    events = glob.glob(os.path.join(run_dir, BEHAVIOR, "events.out.tfevents*"))
    if not os.path.isdir(run_dir) or not events:
        continue

    shapes, issues = "-", []
    try:
        model = onnx.load(os.path.join(run_dir, f"{BEHAVIOR}.onnx"))
        shapes = " + ".join("x".join(str(d.dim_value) for d in i.type.tensor_type.shape.dim[1:])
                            for i in model.graph.input if i.name.startswith("obs_"))
    except Exception:
        issues.append("no final model")

    text = ""
    try:
        text = open(os.path.join(run_dir, "configuration.yaml"), encoding="utf-8").read()
    except Exception:
        issues.append("no configuration.yaml")
    bad = [k for k, v in STANDARD.items() if (re.search(rf"\b{k}:\s*(\S+)", text) or [None, None])[1] != v]
    curriculum = "MazeSize" in text
    if curriculum != ("curriculum" in run.lower() or "_curr" in run.lower()):
        bad.append("curriculum")

    points = []
    for ev in events:
        acc = EventAccumulator(ev, size_guidance={"scalars": 0})
        acc.Reload()
        if REWARD_TAG in acc.Tags()["scalars"]:
            points += acc.Scalars(REWARD_TAG)
    points.sort(key=lambda e: e.wall_time)
    gaps = [b.wall_time - a.wall_time for a, b in zip(points, points[1:])]
    usual = sorted(gaps)[len(gaps) // 2] if gaps else 0
    hours = sum(g for g in gaps if g <= 10 * usual) / 3600
    final = sum(e.value for e in points[-10:]) / max(len(points[-10:]), 1)
    last_step = max(e.step for e in points)
    if last_step < args.expected_steps * 0.99:
        issues.append(f"stopped at {last_step:,}")

    listed = set()
    try:
        status = json.load(open(os.path.join(run_dir, "run_logs", "training_status.json")))
        listed = {re.split(r"[\\/]", c["file_path"])[-1] for c in status[BEHAVIOR]["checkpoints"]}
    except Exception:
        pass
    stale = sorted({os.path.basename(p) for p in glob.glob(os.path.join(run_dir, BEHAVIOR, f"{BEHAVIOR}-*.onnx"))} - listed)

    t0 = datetime.datetime.fromtimestamp(points[0].wall_time) if points else None
    print(f"{run:24s} {shapes:16s} {'ok' if not bad else 'BAD':4s} "
          f"{t0:%m-%d %H:%M} {hours:5.2f} {len(points):4d} {final:7.3f}  "
          f"{', '.join(stale) if stale else '-'}"
          + (f"   <- {'; '.join(issues + (['config: ' + ','.join(bad)] if bad else []))}" if issues or bad else ""))
    problems += bool(issues or bad or stale)

print(f"\n{problems} run(s) need a look." if problems else "\nAll runs look fine.")
