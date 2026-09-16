using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;

[RequireComponent(typeof(MazeConstructor))]

public class GameController : MonoBehaviour
{
    [SerializeField] private int m_sizeRows = 13, m_sizeCols = 15;
    [SerializeField] private bool examinationMode = false;

    public int sizeRows
    { 
        get { return m_sizeRows; } private set { m_sizeRows = value; }
    }

    public int sizeCols
    {
        get { return m_sizeCols; } private set { m_sizeCols = value; }
    }

    private MazeConstructor generator;

    private int completedMazeCount, completionsToMazeChange = 5;
    private bool goalReached;
    private EnvironmentParameters env;
    float time;

    void Start() 
    {
        env = Academy.Instance.EnvironmentParameters;
        generator = GetComponent<MazeConstructor>();
        if(!examinationMode)
        {
            int envParam = (int)env.GetWithDefault("MazeSize", 19);
            ChangeMazeSize(envParam, envParam+2);
        }
        StartNewGame();
    }

    void FixedUpdate() 
    {
        time += Time.fixedDeltaTime;
        if(goalReached && time > 3) goalReached = false;
    }

    /// <summary>
    /// Starts the game. If examinationMode is true, starts the examination mode.
    /// </summary>
    public void StartNewGame()
    {
        if(!examinationMode) generator.GenerateAllMazes(sizeRows, sizeCols);
        else generator.GenerateExaminationMazes(sizeRows, sizeCols);
    }

    /// <summary>
    /// Changes the next generated mazes size.
    /// </summary>
    /// <param name="newRowSize"></param>
    /// <param name="newColSize"></param>
    public void ChangeMazeSize(int newRowSize, int newColSize)
    {
        sizeRows = newRowSize;
        sizeCols = newColSize;
    }

    /// <summary>
    /// Used to create a new maze for a specific agent.
    /// </summary>
    /// <param name="environment"></param>
    public void CreateNewMaze(int environment)
    {
        if(examinationMode && !goalReached)
        {
            goalReached = true;
            generator.GenerateExaminationMazes(sizeRows, sizeCols);
        }
        else
        {
            int envParam = (int)env.GetWithDefault("MazeSize", 19);
            ChangeMazeSize(envParam, envParam+2);
            generator.GenerateSingleMaze(sizeRows, sizeCols, environment);
        }
    }
}
