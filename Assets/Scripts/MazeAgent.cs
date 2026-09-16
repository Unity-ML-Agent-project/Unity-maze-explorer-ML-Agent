using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class MazeAgent : Agent
{
    [SerializeField]
    private float step = 3.75f;
    [SerializeField]
    private int mazeCountToChange = 5;

    private Transform hitGoal;
    private LayerMask mask;
    private int width, height, stepsUntilZero, mazesTillChange = -1;

    public Transform startPos;
    public List<Transform> goals;
    public bool evaluationMode = false;
    private bool pausedActions;

    public override void OnEpisodeBegin()
    {
        mazesTillChange++;

        // Request a new maze layout if threshold is reached
        if (mazesTillChange >= mazeCountToChange && !evaluationMode)
        {
            mazesTillChange = 0;
            SendMessageUpwards("CreateNewMaze", transform.parent.GetSiblingIndex());
        }

        stepsUntilZero = MaxStep;
        GameController gc = transform.GetComponentInParent<GameController>();
        width = gc.sizeCols;
        height = gc.sizeRows;
        mask = ~LayerMask.GetMask("Object");

        // Reset agent position
        if (startPos != null)
        {
            Vector3 pos = Vector3.zero;
            pos.x = startPos.localPosition.x;
            pos.y = transform.localPosition.y;
            pos.z = startPos.localPosition.z;

            transform.localPosition = pos;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (pausedActions) return;

        stepsUntilZero--;

        // Check goal collision and assign reward
        if (IsCloseToGoal())
        {
            if (hitGoal != null)
            {
                SendMessageUpwards("IncreaseScore", transform.parent.GetSiblingIndex(), SendMessageOptions.DontRequireReceiver);

                if (MaxStep != 0) AddReward((float)stepsUntilZero / MaxStep);

                SendMessageUpwards("ResetGoal", new System.Tuple<int, int>(transform.parent.GetSiblingIndex(), hitGoal.GetSiblingIndex()));
                goals.RemoveAt(hitGoal.GetSiblingIndex());

                EndEpisode();
            }
            hitGoal = null;
        }

        // Handle discrete movement actions
        ActionSegment<int> disc = actions.DiscreteActions;
        Vector3 movement = Vector3.zero;

        switch (disc[0])
        {
            case 1:
                if (!Physics.Raycast(transform.position, Vector3.forward, step, mask)) movement.z += step;
                break;
            case 2:
                if (!Physics.Raycast(transform.position, Vector3.back, step, mask)) movement.z -= step;
                break;
            case 3:
                if (!Physics.Raycast(transform.position, Vector3.right, step, mask)) movement.x += step;
                break;
            case 4:
                if (!Physics.Raycast(transform.position, Vector3.left, step, mask)) movement.x -= step;
                break;
        }

        transform.localPosition += movement;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        base.CollectObservations(sensor);

        // GridSensor and CameraSensor collect data automatically.
        // Ensure "Space Size" is set to 0 in Behavior Parameters.
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> disc = actionsOut.DiscreteActions;

        if (Input.GetKey(KeyCode.W)) disc[0] = 1;
        if (Input.GetKey(KeyCode.S)) disc[0] = 2;
        if (Input.GetKey(KeyCode.D)) disc[0] = 3;
        if (Input.GetKey(KeyCode.A)) disc[0] = 4;
    }

    // Check if agent is close to the goal
    private bool IsCloseToGoal()
    {
        foreach (Transform tran in goals)
        {
            if (Vector3.Distance(transform.localPosition, tran.localPosition) < 3)
            {
                hitGoal = tran;
                return true;
            }
        }
        return false;
    }

    public void SetPausedActions(bool value)
    {
        pausedActions = value;
    }
}