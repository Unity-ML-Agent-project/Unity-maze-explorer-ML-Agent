"""
Sanity check for the environment itself: act randomly and count how often agents reach the goal.
Scenes with the same maze setup should give similar numbers regardless of sensor.

Usage:  python Tools/random_rollout.py --steps 20000      (then press Play in Unity)
"""
import argparse
import time

import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

parser = argparse.ArgumentParser()
parser.add_argument("--env", default=None, help="build executable; omit to connect to the Editor")
parser.add_argument("--steps", type=int, default=20000, help="environment steps to run")
parser.add_argument("--unity-args", default="", help='extra Unity player arguments, e.g. "-force-vulkan"')
args = parser.parse_args()

engine = EngineConfigurationChannel()
print("Waiting for Unity" + ("" if args.env else ": press Play now..."))
env = UnityEnvironment(file_name=args.env, base_port=5010 if args.env else 5004, side_channels=[engine],
                       additional_args=args.unity_args.split())
engine.set_configuration_parameters(time_scale=20)
try:
    env.reset()
    behavior = list(env.behavior_specs)[0]
    spec = env.behavior_specs[behavior]

    episodes, successes, returns = 0, 0, []
    t0 = time.time()
    for step in range(1, args.steps + 1):
        decision, terminal = env.get_steps(behavior)
        for r, interrupted in zip(terminal.reward, terminal.interrupted):
            episodes += 1
            if r > 0:
                successes += 1
                returns.append(float(r))
        if len(decision) > 0:
            env.set_actions(behavior, spec.action_spec.random_action(len(decision)))
        env.step()
        if step % 2000 == 0:
            print(f"step {step}: episodes={episodes} reached_goal={successes} "
                  f"({step / (time.time() - t0):.1f} env steps/s)")

    print(f"\nRESULT: {episodes} episodes, {successes} reached the goal "
          f"({100 * successes / max(episodes, 1):.1f}%)"
          + (f", mean reward on success {np.mean(returns):.3f}" if returns else ""))
finally:
    env.close()
