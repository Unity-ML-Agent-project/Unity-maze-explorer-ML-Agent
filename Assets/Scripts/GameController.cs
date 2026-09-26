using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;

[RequireComponent(typeof(MazeConstructor))]

public class GameController : MonoBehaviour
{
    /// <summary>
    /// Random: unrestricted maze generation (original behaviour).
    /// Seen: mazes come from a small fixed seed pool. Training draws from it at random; the
    /// evaluation walks it in a fixed shuffled order that is the same for every model.
    /// Unseen: seeds outside the pool, i.e. layouts never trained on. Evaluation walks them in
    /// ascending order so every model is tested on exactly the same mazes.
    /// The mode can be overridden by the "SeedMode" environment parameter (0/1/2) from the
    /// training yaml, so one build serves both the Random and the Seen runs.
    /// </summary>
    public enum MazeSeedMode { Random = 0, Seen = 1, Unseen = 2 }

    [SerializeField] private int m_sizeRows = 13, m_sizeCols = 15;
    [SerializeField] private bool examinationMode = false;

    [Header("Generalization Gap")]
    [SerializeField] private MazeSeedMode seedMode = MazeSeedMode.Random;
    // Seen pool is small on purpose: a run only draws ~500 mazes, so a large pool would leave most
    // "seen" mazes never actually trained on. Unseen range never overlaps the pool.
    // Constants on purpose: as serialized fields Unity kept the stale defaults cached in the imported
    // prefab, so the first Linux builds silently trained on the old pool (seeds 0-999).
    private const int seenPoolBaseSeed = 0;
    private const int seenPoolSize = 50;
    private const int unseenRangeStart = 1000;
    private const int unseenRangeSize = 100;

    // Fixed so the shuffled Seen evaluation order is identical for every sensor and every run.
    private const int SeenOrderShuffleSeed = 2024;
    private int[] seenEvalOrder;
    private int examRound;

    /// <summary>Seed used for the most recently generated maze (-1 before the first seeded one).</summary>
    public int LastSeed { get; private set; } = -1;

    /// <summary>Environment parameter "SeedMode" wins over the Inspector value when present.</summary>
    private MazeSeedMode ActiveSeedMode => env == null
        ? seedMode
        : (MazeSeedMode)(int)env.GetWithDefault("SeedMode", (float)(int)seedMode);

    public MazeSeedMode SeedMode => ActiveSeedMode;

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
        bool seeded = ActiveSeedMode != MazeSeedMode.Random;
        if(!examinationMode) generator.GenerateAllMazes(sizeRows, sizeCols, seeded ? (System.Func<int>)NextSeed : null);
        else generator.GenerateExaminationMazes(sizeRows, sizeCols, seeded ? NextSeed() : (int?)null);
    }

    /// <summary>
    /// Picks the seed for the next maze. Only called when the seed mode isn't Random.
    /// Training: random draw from the Seen pool. Evaluation: the round-th entry of a fixed
    /// sequence, so every model faces the same mazes in the same order.
    /// </summary>
    private int NextSeed()
    {
        int seed;
        if(examinationMode)
        {
            int round = examRound++;
            seed = ActiveSeedMode == MazeSeedMode.Seen
                ? SeenEvalOrder()[round % seenPoolSize]
                : unseenRangeStart + round % unseenRangeSize;
        }
        else
        {
            seed = ActiveSeedMode == MazeSeedMode.Seen
                ? UnityEngine.Random.Range(seenPoolBaseSeed, seenPoolBaseSeed + seenPoolSize)
                : UnityEngine.Random.Range(unseenRangeStart, unseenRangeStart + unseenRangeSize);
        }

        LastSeed = seed;
        // Shows up as "Maze/Seed" in TensorBoard: proves which seed range the mazes really came from.
        Academy.Instance.StatsRecorder.Add("Maze/Seed", seed);
        return seed;
    }

    /// <summary>The Seen pool in a fixed shuffled order (Fisher-Yates with a constant seed).</summary>
    private int[] SeenEvalOrder()
    {
        if(seenEvalOrder != null && seenEvalOrder.Length == seenPoolSize) return seenEvalOrder;

        seenEvalOrder = new int[seenPoolSize];
        for(int i = 0; i < seenPoolSize; i++) seenEvalOrder[i] = seenPoolBaseSeed + i;

        System.Random rng = new System.Random(SeenOrderShuffleSeed);
        for(int i = seenPoolSize - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = seenEvalOrder[i];
            seenEvalOrder[i] = seenEvalOrder[j];
            seenEvalOrder[j] = tmp;
        }
        return seenEvalOrder;
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
        bool seeded = ActiveSeedMode != MazeSeedMode.Random;
        if(examinationMode && !goalReached)
        {
            goalReached = true;
            generator.GenerateExaminationMazes(sizeRows, sizeCols, seeded ? NextSeed() : (int?)null);
        }
        else
        {
            int envParam = (int)env.GetWithDefault("MazeSize", 19);
            ChangeMazeSize(envParam, envParam+2);
            generator.GenerateSingleMaze(sizeRows, sizeCols, environment, seeded ? NextSeed() : (int?)null);
        }
    }
}
