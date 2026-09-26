# Training Stability runs (Item 3)

Goal: retrain the same setup (non-curriculum) with several fixed random seeds and report Mean ± SD of the
final reward per sensor.

- Sensors: Vector (`MazeRunnerVector` scene), Raycast (`MazeRunnerRaycastSensor` scene)
- Seeds (same list for both sensors): `42, 123, 456, 789, 1024`
- Config: `Assets/Config/MazeConfig.yaml` (no curriculum, no seed in the file - the seed is passed as `--seed`)
- Job list: [`jobs_training_stability.txt`](jobs_training_stability.txt) - the single source of truth for which runs exist

## Which runs count

All 10 runs must be trained in the **same environment**, otherwise the SD mixes seed variance with version differences.
The official environment is the shared server env (`mlagents`, mlagents 1.2.0.dev0), Unity 2022.3.62f3.

The runs already in `results/` (`vector_seed42`, `raycast_seed*`) were trained locally on Windows with mlagents 1.0.0.
Keep them as a reference only; do not average them with the server runs.

## How to run (on the shared server)

Read the server guide first (3 concurrent trainings for the whole team, never `pkill -f mlagents-learn`).

1. Unity 2022.3.62f3 with Linux Build Support (Mono). Build each scene separately (File > Build Settings > Linux,
   only that scene ticked, Development Build off) into `Builds/Vector/MazeVector` and `Builds/Raycast/MazeRaycast`.
   The executable names must match the job list.
2. Upload to your own folder `~/maze_<name>` (build folders, `Assets/Config/MazeConfig.yaml` -> `config/`, this job list),
   then `chmod +x` the executables and `cp ~/tools/run_queue.sh ~/maze_<name>/`.
3. Check free slots: `pgrep -fa "mlagents-learn" | grep -o -- "--run-id=[^ ]*\|--base-port=[0-9]*" | paste - - | sort -u ; uptime`
4. Start (background), using your own base port:
   `cd ~/maze_<name> && nohup bash run_queue.sh jobs_training_stability.txt 1 <base_port> > queue.log 2>&1 &`

| Person | base_port |
|---|---|
| Moonnight | 5100 |
| Evan | 7000 |
| chiheo | 8000 |
| others | 9000 |

To split the work, copy the job list and delete the lines someone else runs.

5. Verify the seed was applied: `grep seed ~/maze_<name>/results/<run-name>/configuration.yaml` must show the seed of that line, not `-1`.
6. Copy results back: `scp -r student@100.67.64.25:~/maze_<name>/results "<repo>\results_server"`

## Status (server runs)

| Run | Status |
|---|---|
| vector_seed42 / 123 / 456 / 789 / 1024 | reported complete (chiheo); copy to `results_server/` |
| raycast_seed42 / 123 / 456 / 789 / 1024 | reported complete (chiheo); copy to `results_server/` |

# Generalization Gap (Item 4)

Question: did the agent learn to solve mazes, or only the mazes it trained on?
`Gap = Seen success rate - Unseen success rate`, always reported together with both success rates and 95% CIs
(a small gap is meaningless when both are low).

## Design

| | |
|---|---|
| Seen mazes | seeds `0-49` (50 mazes). Training draws from this pool at random; evaluation walks it in a fixed shuffled order (same for every model) |
| Unseen mazes | seeds `1000-1099`, evaluated in ascending order (identical for every model), never used in training |
| Maze size | 19 x 21 everywhere (training final size = `ExaminationScene` size; a seed only reproduces a maze at the same size) |
| Success | reaches the goal within 60 s of game time; 100 rounds per Seen / Unseen evaluation |
| Evaluation | one model at a time (not the shared 4-agent race) |
| Training | seed `42`, 10 repeats per condition (run-ids `_r1`-`_r10`). The repeats catch failed trainings (reward stuck at 0), so report the training success rate; they differ only by run-to-run nondeterminism, not by seed |
| Conditions | Vector, Raycast, Hybrid-Abs, Hybrid-Rel, no curriculum = 4 x 10 = 40 runs. Curriculum is excluded: it changes the maze size, and a seed only reproduces a maze at one size |

The Seen pool is small on purpose: one training run only draws about 500 mazes, so a pool of 1000 would leave most "seen"
mazes never actually trained on. Item 3 (`jobs_training_stability.txt`, Random mode) is separate and not reused here.

## How the mode is selected

`GameController` reads the environment parameter `SeedMode` (0 = Random, 1 = Seen, 2 = Unseen) from the training yaml, so one
build serves both Item 3 and Item 4. The Item 4 yamls are in [`config/`](config/): `MazeConfigSeen.yaml`; the
`_HybridAbs` / `_HybridRel` variants also set `HybridMode` (0 = absolute position,
1 = position relative to the goal). Do not edit `Assets/Config/MazeConfig.yaml`: the Item 3 results depend on it.

## How to run

Same steps as above, with these differences:
- upload `training/config/*.yaml` to `~/maze_<name>/config/` and use [`jobs_generalization.txt`](jobs_generalization.txt)
- Vector/Raycast executables are built from branch `chiheo`; the Hybrid executable from branch `chiheo-hybrid`
  (scene `MazeRunnerHybrid`, output `Builds/Hybrid/MazeHybrid`)
- 40 runs is a lot for 3 shared slots: split the job list between people, or drop lines
- verify in `results/<name>/configuration.yaml`: `seed: 42` and `environment_parameters` contains `SeedMode: 1.0`; in TensorBoard `Maze/Seed` must stay within 0-49

## Known limits

- The first maze of each environment is generated before the yaml parameters arrive, so it may not come from the Seen pool
  (a handful of the ~500 mazes).
- A training run whose reward stays at 0 has learned nothing: report the training success rate (thesis criterion: reward
  consistently above 0 during the last 200,000 steps) and take the Seen/Unseen gap from the successful runs only.
- The evaluation runner is not built yet. It will be validated on the first finished models (needs the models to test against).
  `ExaminationManager` already writes `ExaminationTimes_Seen.csv` / `ExaminationTimes_Unseen.csv` with a `Seed` column.
