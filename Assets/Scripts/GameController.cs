using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;

[RequireComponent(typeof(MazeConstructor))]

public class GameController : MonoBehaviour
{
    /// <summary>
    /// Random: unrestricted maze generation (original behaviour).
    /// Seen: mazes are drawn from a fixed, reused seed pool - the pool a curriculum/training
    /// run is configured with here.
    /// Unseen: mazes are drawn from seeds outside that pool, i.e. layouts never trained on.
    /// Seen/Unseen only matter together: train with Seen, then run the evaluation scene once
    /// per mode to get comparable Seen-maze vs Unseen-maze success rates for the
    /// generalization-gap metric.
    /// </summary>
    public enum MazeSeedMode { Random, Seen, Unseen }

    [SerializeField] private int m_sizeRows = 13, m_sizeCols = 15;
    [SerializeField] private bool examinationMode = false;

    [Header("Generalization Gap")]
    [SerializeField] private MazeSeedMode seedMode = MazeSeedMode.Random;
    // Training seeds: 0-999. Test seeds: 1000-1199 - adjacent to, but never overlapping, the
    // training range, so "unseen" is guaranteed disjoint from anything the agent trained on.
    [SerializeField] private int seenPoolBaseSeed = 0;
    [SerializeField] private int seenPoolSize = 1000;
    [SerializeField] private int unseenRangeStart = 1000;
    [SerializeField] private int unseenRangeSize = 200;

    public MazeSeedMode SeedMode => seedMode;

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
        if(!examinationMode) generator.GenerateAllMazes(sizeRows, sizeCols, seedMode == MazeSeedMode.Random ? null : (System.Func<int>)NextSeed);
        else generator.GenerateExaminationMazes(sizeRows, sizeCols, seedMode == MazeSeedMode.Random ? (int?)null : NextSeed());
    }

    /// <summary>
    /// Draws the next seed according to <see cref="seedMode"/>: a value from the fixed "seen"
    /// pool, or a value from a disjoint range that pool never touches ("unseen"). Only meant to
    /// be called when seedMode isn't Random.
    /// </summary>
    private int NextSeed()
    {
        return seedMode == MazeSeedMode.Seen
            ? UnityEngine.Random.Range(seenPoolBaseSeed, seenPoolBaseSeed + seenPoolSize)
            : UnityEngine.Random.Range(unseenRangeStart, unseenRangeStart + unseenRangeSize);
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
            generator.GenerateExaminationMazes(sizeRows, sizeCols, seedMode == MazeSeedMode.Random ? (int?)null : NextSeed());
        }
        else
        {
            int envParam = (int)env.GetWithDefault("MazeSize", 19);
            ChangeMazeSize(envParam, envParam+2);
            generator.GenerateSingleMaze(sizeRows, sizeCols, environment, seedMode == MazeSeedMode.Random ? (int?)null : NextSeed());
        }
    }
}
