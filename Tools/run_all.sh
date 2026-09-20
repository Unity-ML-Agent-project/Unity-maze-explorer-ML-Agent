#!/bin/bash
# Runs the Grid/Camera trainings on the lab server, PARALLEL at a time.
#
# Usage:   cd ~/maze && nohup bash tools/run_all.sh [FIRST] [LAST] > run_all.log 2>&1 &
#          e.g. "tools/run_all.sh 6 40" runs repetitions 06..40 of all four groups
#          (default 1 5). Repetitions are interleaved so every batch mixes Grid and Camera.
# Stop:    pkill -f maze-runall   (then stop ports 5100-5399, see the team guide)
#
# Re-running skips finished runs (results/<id>/MazeAgent.onnx exists) and
# restarts unfinished ones from scratch.

FIRST=${1:-1}
LAST=${2:-5}
PARALLEL=3
PORT_BASE=5100      # this user's range is 5100-5399
PORT_SLOTS=30       # ports are reused cyclically: 5100, 5110, ... 5390

cd ~/maze || exit 1
export DISPLAY=:1 XAUTHORITY=/run/user/1000/gdm/Xauthority
source ~/miniforge3/etc/profile.d/conda.sh && conda activate mlagents
mkdir -p logs results
export PORT_BASE PORT_SLOTS

# run-id, config, executable
jobs() {
    for i in $(seq "$FIRST" "$LAST"); do
        n=$(printf "%02d" "$i")
        echo "grid_$n MazeConfig.yaml Grid/MazeGrid.x86_64"
        echo "camera_$n MazeConfig.yaml Camera/MazeCamera.x86_64"
        echo "grid_curriculum_$n MazeConfigCurriculum.yaml Grid/MazeGrid.x86_64"
        echo "camera_curriculum_$n MazeConfigCurriculum.yaml Camera/MazeCamera.x86_64"
    done
}

run_one() {
    local idx=$1 id=$2 cfg=$3 exe=$4
    local port=$((PORT_BASE + (idx % PORT_SLOTS) * 10))
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

echo "$(date '+%F %T') QUEUE repetitions $FIRST..$LAST, $PARALLEL at a time"
jobs | nl -nln -w1 -s' ' \
    | xargs -P "$PARALLEL" -L 1 bash -c 'run_one "$@"' maze-runall
echo "$(date '+%F %T') ALL JOBS FINISHED"
