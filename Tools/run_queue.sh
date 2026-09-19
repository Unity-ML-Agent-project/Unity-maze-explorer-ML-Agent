#!/bin/bash
# Generic training queue for the shared lab server.
#
# Usage (run inside YOUR OWN folder, e.g. ~/maze_evan):
#   nohup bash run_queue.sh jobs.txt <parallel> <base_port> > queue.log 2>&1 &
#
# jobs.txt: one training per line
#   <run-id> <config yaml> <executable> [extra mlagents-learn args...]
# e.g.
#   vector_01 config/MazeConfig.yaml Vector/MazeVector.x86_64
#   raycast_01 config/MazeConfig.yaml Raycast/MazeRaycast.x86_64 --no-graphics
#
# - Finished runs (results/<run-id>/MazeAgent.onnx exists) are skipped.
# - Unfinished leftovers are restarted from scratch.
# - Every job gets its own port: base_port + 10 * line number.
#   Pick a base_port range nobody else uses (see the team guide).

JOBS_FILE=$1
PARALLEL=${2:-1}
BASE_PORT=${3:?"usage: run_queue.sh jobs.txt <parallel> <base_port>"}
CONDA_ENV=${CONDA_ENV:-mlagents}

[ -f "$JOBS_FILE" ] || { echo "jobs file not found: $JOBS_FILE"; exit 1; }

export DISPLAY=:1 XAUTHORITY=/run/user/1000/gdm/Xauthority
source ~/miniforge3/etc/profile.d/conda.sh && conda activate "$CONDA_ENV" || exit 1
mkdir -p logs results
export BASE_PORT

run_one() {
    local idx=$1 id=$2 cfg=$3 exe=$4
    shift 4
    local port=$((BASE_PORT + idx * 10))
    if [ -f "results/$id/MazeAgent.onnx" ]; then
        echo "$(date '+%F %T') SKIP  $id (already finished)"
        return
    fi
    local force=""
    [ -d "results/$id" ] && force="--force"
    echo "$(date '+%F %T') START $id (port $port)"
    mlagents-learn "$cfg" --env="$exe" --run-id="$id" --base-port="$port" $force "$@" \
        > "logs/$id.log" 2>&1
    if [ -f "results/$id/MazeAgent.onnx" ]; then
        echo "$(date '+%F %T') DONE  $id"
    else
        echo "$(date '+%F %T') FAIL  $id (see logs/$id.log)"
    fi
}
export -f run_one

grep -v -E '^\s*(#|$)' "$JOBS_FILE" | nl -nln -w1 -s' ' \
    | xargs -P "$PARALLEL" -L 1 bash -c 'run_one "$@"' "runq-$BASE_PORT"
echo "$(date '+%F %T') ALL JOBS FINISHED"
