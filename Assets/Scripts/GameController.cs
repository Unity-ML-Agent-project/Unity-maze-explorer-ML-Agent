using UnityEngine;
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

    private bool goalReached;
    private EnvironmentParameters env;
    float time;

    void Awake()
    {
        generator = GetComponent<MazeConstructor>();
    }

    void Start()
    {
        env = Academy.Instance.EnvironmentParameters;
        if (!examinationMode) ApplyMazeSizeParam();
        StartNewGame();
    }

    void FixedUpdate()
    {
        time += Time.fixedDeltaTime;
        if (goalReached && time > 3) goalReached = false;
    }

    /// <summary>
    /// Starts the game. If examinationMode is true, starts the examination mode.
    /// </summary>
    public void StartNewGame()
    {
        if (!examinationMode) generator.GenerateAllMazes(sizeRows, sizeCols);
        else generator.GenerateExaminationMazes(sizeRows, sizeCols);
    }

    /// <summary>
    /// Changes the next generated mazes size.
    /// </summary>
    public void ChangeMazeSize(int newRowSize, int newColSize)
    {
        sizeRows = newRowSize;
        sizeCols = newColSize;
    }

    /// <summary>
    /// Reads the curriculum parameter "MazeSize" (rows = MazeSize, cols = MazeSize + 2).
    /// </summary>
    private void ApplyMazeSizeParam()
    {
        int envParam = (int)env.GetWithDefault("MazeSize", 19);
        ChangeMazeSize(envParam, envParam + 2);
    }

    /// <summary>
    /// Used to create a new maze for a specific agent.
    /// </summary>
    public void CreateNewMaze(int environment)
    {
        if (examinationMode)
        {
            if (goalReached) return;
            goalReached = true;
            generator.GenerateExaminationMazes(sizeRows, sizeCols);
        }
        else
        {
            ApplyMazeSizeParam();
            generator.GenerateSingleMaze(sizeRows, sizeCols, environment);
        }
    }
}
