# Results directory notes

Everything under `results/` was trained locally on Windows (mlagents 1.0.0). Server runs are copied to `results_server/`
(mlagents 1.2.0.dev0); see [`../training/README.md`](../training/README.md) for how they are produced.

## Seed control status

| Run set | Sensor | Curriculum | `env_settings.seed` | Reproducible? |
|---|---|---|---|---|
| `vector_01` ~ `vector_05` | Vector | No | `-1` (auto, unrecorded) | **No** - the actual seed was never logged (not in `configuration.yaml`, `run_logs/timers.json`, or `run_logs/training_status.json`). |
| `raycast_01` ~ `raycast_05` | Raycast | No | `-1` (auto, unrecorded) | **No** - same as above. |
| `vector_curriculum_01` ~ `05`, `raycast_curriculum_01` ~ `05` | Vector / Raycast | Yes | `-1` (auto, unrecorded) | **No** - same caveat. |
| `vector_seed42`, `raycast_seed{42,123,456,789,1024}` | Vector / Raycast | No | explicit | Yes, but trained locally with mlagents 1.0.0: **reference only**, not for the official Mean ± SD. |

## Why the seed matters

`env_settings.seed` isn't just a Python/PyTorch detail: Unity ML-Agents' `Academy.cs` calls
`UnityEngine.Random.InitState(seed)` with this exact value when the environment connects, so it
also seeds the maze generation (`MazeDataGenerator`/`GameController`). A run made with `seed: -1`
has no recorded, reproducible seed at any level - not network init, not exploration, not which
mazes got generated.

## Training Stability analysis

The official Mean ± SD uses only the 10 server runs (`vector_seed{42,123,456,789,1024}` and
`raycast_seed{42,123,456,789,1024}` in `results_server/`): same environment, same seed list for both sensors.
Local runs in this folder must not be averaged into it.
