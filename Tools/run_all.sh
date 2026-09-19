#!/bin/bash
# Runs all 20 Grid/Camera trainings on the lab server, PARALLEL at a time.
# Start:   cd ~/maze && nohup bash tools/run_all.sh > run_all.log 2>&1 &
# Re-running skips finished runs (results/<id>/MazeAgent.onnx exists) and
# restarts unfinished ones from scratch.

PARALLEL=3

cd ~/maze || exit 1
export DISPLAY=:1 XAUTHORITY=/run/user/1000/gdm/Xauthority
source ~/miniforge3/etc/profile.d/conda.sh && conda activate mlagents
mkdir -p logs results

# run-id, config, executable
JOBS="
grid_01 MazeConfig.yaml Grid/MazeGrid.x86_64
grid_02 MazeConfig.yaml Grid/MazeGrid.x86_64
grid_03 MazeConfig.yaml Grid/MazeGrid.x86_64
grid_04 MazeConfig.yaml Grid/MazeGrid.x86_64
grid_05 MazeConfig.yaml Grid/MazeGrid.x86_64
grid_curriculum_01 MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64
grid_curriculum_02 MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64
grid_curriculum_03 MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64
grid_curriculum_04 MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64
grid_curriculum_05 MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64
camera_01 MazeConfig.yaml Camera/MazeCamera.x86_64
camera_02 MazeConfig.yaml Camera/MazeCamera.x86_64
camera_03 MazeConfig.yaml Camera/MazeCamera.x86_64
camera_04 MazeConfig.yaml Camera/MazeCamera.x86_64
camera_05 MazeConfig.yaml Camera/MazeCamera.x86_64
camera_curriculum_01 MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64
camera_curriculum_02 MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64
camera_curriculum_03 MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64
camera_curriculum_04 MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64
camera_curriculum_05 MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64
"

run_one() {
    local idx=$1 id=$2 cfg=$3 exe=$4
    local port=$((5100 + idx * 10))   # every job gets its own port
    if [ -f "results/$id/MazeAgent.onnx" ]; then
        echo "$(date '+%F %T') SKIP  $id (already finished)"
        return
    fi
    local force=""
    [ -d "results/$id" ] && force="--force"   # unfinished leftover: start over
    # Camera needs Vulkan: the default OpenGL path renders on the CPU (llvmpipe) on this server
    local gfx=""
    [[ "$exe" == Camera/* ]] && gfx="--env-args -force-vulkan"
    echo "$(date '+%F %T') START $id (port $port)"
    mlagents-learn "config/$cfg" --env="$exe" --run-id="$id" --base-port="$port" $force $gfx \
        > "logs/$id.log" 2>&1
    if [ -f "results/$id/MazeAgent.onnx" ]; then
        echo "$(date '+%F %T') DONE  $id"
    else
        echo "$(date '+%F %T') FAIL  $id (see logs/$id.log)"
    fi
}
export -f run_one

echo "$JOBS" | grep -v '^$' | nl -nln -w1 -s' ' \
    | xargs -P "$PARALLEL" -L 1 bash -c 'run_one "$@"' maze-runall
echo "$(date '+%F %T') ALL JOBS FINISHED"
