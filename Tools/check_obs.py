"""
Save what the agents actually observe as PNG images, to verify Camera/Grid sensors.

Usage:
  Editor:  python Tools/check_obs.py --out obs_check      (then press Play in Unity)
  Build:   python Tools/check_obs.py --env <path to build executable> --out obs_check

A Camera sensor that renders nothing (e.g. -nographics / Server Build) shows up
here as an all-black image and is reported as BLANK.
"""
import argparse
import os

import numpy as np
from PIL import Image
from mlagents_envs.environment import UnityEnvironment

WARMUP_STEPS = 20
MAX_AGENTS = 4
UPSCALE = 4

parser = argparse.ArgumentParser()
parser.add_argument("--env", default=None, help="build executable; omit to connect to the Editor")
parser.add_argument("--out", default="obs_check", help="folder for the PNG images")
parser.add_argument("--unity-args", default="", help='extra Unity player arguments, e.g. "-force-vulkan"')
args = parser.parse_args()

file_name = args.env
out_dir = args.out
os.makedirs(out_dir, exist_ok=True)

if file_name is None:
    print("Waiting for the Unity Editor: press Play now...")
env = UnityEnvironment(file_name=file_name, base_port=5010 if file_name else 5004, no_graphics=False,
                       additional_args=args.unity_args.split(), log_folder=os.path.abspath(out_dir))
try:
    env.reset()
    behavior = list(env.behavior_specs)[0]
    spec = env.behavior_specs[behavior]
    print(f"Behavior: {behavior}")
    for i, o in enumerate(spec.observation_specs):
        print(f"  obs_{i}: shape={o.shape} name={o.name}")

    # Let the mazes generate and the agents move a little
    for _ in range(WARMUP_STEPS):
        decision, _ = env.get_steps(behavior)
        if len(decision) > 0:
            env.set_actions(behavior, spec.action_spec.random_action(len(decision)))
        env.step()

    decision, _ = env.get_steps(behavior)
    all_ok = True
    for i, obs in enumerate(decision.obs):
        if obs.ndim != 4:
            print(f"obs_{i}: vector observation {obs.shape}, skipped")
            continue
        for a in range(min(len(decision), MAX_AGENTS)):
            chw = obs[a]
            for c in range(chw.shape[0]):
                ch = chw[c]
                blank = float(ch.max() - ch.min()) < 1e-3
                all_ok &= not blank
                print(f"obs_{i} agent{a} ch{c}: min={ch.min():.3f} max={ch.max():.3f} "
                      f"mean={ch.mean():.3f} {'BLANK!' if blank else 'ok'}")
                if a == 0 and max(ch.shape) <= 32:
                    # Small grids (GridSensor) are easier to read as text, same layout as the PNG
                    print("\n".join("".join("#" if v > 0.5 else "." for v in row) for row in ch))
                img = Image.fromarray((np.clip(ch, 0, 1) * 255).astype(np.uint8))
                img = img.resize((ch.shape[1] * UPSCALE, ch.shape[0] * UPSCALE), Image.NEAREST)
                img.save(os.path.join(out_dir, f"obs{i}_agent{a}_ch{c}.png"))
    print(f"\nImages saved to: {os.path.abspath(out_dir)}")
    log_path = os.path.join(out_dir, "Player-0.log")
    if os.path.exists(log_path):
        with open(log_path, errors="replace") as f:
            gfx = [l.strip() for l in f if any(k in l for k in ("Renderer:", "Vendor:", "Vulkan renderer", "Vulkan vendor"))]
        print("Graphics:", *gfx[:6], sep="\n  ")
    print("RESULT:", "OK - sensors see something" if all_ok else "PROBLEM - some channels are blank")
finally:
    env.close()
