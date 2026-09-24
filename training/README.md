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
| vector_seed42 / 123 / 456 / 789 / 1024 | not run yet |
| raycast_seed42 / 123 / 456 / 789 / 1024 | not run yet |
