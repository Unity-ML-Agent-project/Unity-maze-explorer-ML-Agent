using System.Collections.Generic;
using UnityEngine;

public class MazeConstructor : MonoBehaviour
{
    [SerializeField, Tooltip("Toggles whether or not goals are placed at random.\nWhen true and if goal count > 1, goal count is set to 1")]
    private bool randomizeGoals = false;
    public bool showDebug;
    
    [SerializeField] private Material mazeMat1;
    [SerializeField] private Material mazeMat2;
    [SerializeField] private Material startMat;
    [SerializeField] private Material treasureMat;
    [SerializeField] private int goalCount = 4;
    [SerializeField] private float width = 3.75f, height = 3.5f;


    private MazeDataGenerator dataGenerator;
    private MazeMeshGenerator meshGenerator;
    private Transform[] environments;

    // Snapshot of each environment's own maze layout, keyed by environment index.
    // `data` below is reused/overwritten as a scratch field while generating each
    // environment in turn, so anything that needs a specific environment's layout
    // later (e.g. goal reachability, checked well after generation) must read from
    // here instead - reading `data` directly can pick up whichever environment most
    // recently regenerated its maze.
    private int[][,] environmentMazeData;

    private int cols, rows;

    public int[,] data
    {
        get; private set;
    }

    void Awake()
    {
        dataGenerator = new MazeDataGenerator();
        meshGenerator = new MazeMeshGenerator(width, height);
        // default to walls surrounding a single empty cell
        data = new int[,]
        {
            {1, 1, 1},
            {1, 0, 1},
            {1, 1, 1}
        };

        environments = new Transform[transform.childCount];
        for(int i = 0; i < transform.childCount; i++)
        {
            environments[i] = transform.GetChild(i);
        }

        environmentMazeData = new int[environments.Length][,];

        if(!randomizeGoals) goalCount = 1;
    }
    
    /// <summary>
    /// Generates a new maze of the given size into each of the environemnts in the scene
    /// </summary>
    /// <param name="sizeRows"></param>
    /// <param name="sizeCols"></param>
    public void GenerateAllMazes(int sizeRows, int sizeCols)
    {
        if (sizeRows % 2 == 0 && sizeCols % 2 == 0)
        {
            Debug.LogError("Odd numbers work better for dungeon size.");
        }

        rows = sizeRows;
        cols = sizeCols;

        foreach(Transform envi in environments)
        {
            DisposeSingleOldMaze(envi.GetSiblingIndex());

            data = dataGenerator.FromDimensions(sizeRows, sizeCols);
            environmentMazeData[envi.GetSiblingIndex()] = data;

            DisplaySingleMaze(envi.GetSiblingIndex());

            PlaceStartTrigger(envi);
            ResetGoal(envi.GetSiblingIndex());
        }
    }
    
    /// <summary>
    /// Generates a single new maze of the given size to the given environment
    /// </summary>
    /// <param name="sizeRows"></param>
    /// <param name="sizeCols"></param>
    /// <param name="environment"></param>
    public void GenerateSingleMaze(int sizeRows, int sizeCols, int environment)
    {
        if(environment >= environments.Length || environment < 0) return;

        if (sizeRows % 2 == 0 && sizeCols % 2 == 0)
        {
            Debug.LogError("Odd numbers work better for dungeon size.");
        }

        rows = sizeRows;
        cols = sizeCols;

        DisposeSingleOldMaze(environment);

        data = dataGenerator.FromDimensions(sizeRows, sizeCols);
        environmentMazeData[environment] = data;

        DisplaySingleMaze(environment);

        PlaceStartTrigger(environments[environment]);
        ResetGoal(environment);
    }

    /// <summary>
    /// Generates one maze of the given size and then copies it to each environment
    /// </summary>
    /// <param name="sizeRows"></param>
    /// <param name="sizeCols"></param>
    public void GenerateExaminationMazes(int sizeRows, int sizeCols)
    {
        if (sizeRows % 2 == 0 && sizeCols % 2 == 0)
        {
            Debug.LogError("Odd numbers work better for dungeon size.");
        }

        DisposeAllOldMazes();

        rows = sizeRows;
        cols = sizeCols;

        data = dataGenerator.FromDimensions(sizeRows, sizeCols);
        // Examination mode copies one maze layout into every environment, so they
        // all genuinely share this same data.
        for(int i = 0; i < environments.Length; i++)
        {
            environmentMazeData[i] = data;
        }

        DisplayExaminationMazes();

        foreach(Transform envi in environments)
        {
            PlaceStartTrigger(envi);
            ResetGoal(envi.GetSiblingIndex());
        }
    }

    /// <summary>
    /// Handles the creation of a single maze
    /// </summary>
    /// <param name="environment"></param>
    private void DisplaySingleMaze(int environment)
    {
        if(environment >= environments.Length || environment < 0) return;

        GameObject go = new GameObject();
        go.transform.SetParent(environments[environment]);
        go.name = "Procedural Maze";
        go.tag = "Wall";
        go.layer = 9;

        go.transform.localPosition = Vector3.zero;

        float x = width;
        float y = environments[environment].GetChild(0).localScale.y / 2f;
        float z = width;
        environments[environment].GetChild(0).localPosition = new Vector3(x, y, z);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = meshGenerator.FromData(data);
        
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.materials = new Material[2] {mazeMat1, mazeMat2};
    }

    /// <summary>
    /// Handles the creation of a single maze and then creates copies of it
    /// </summary>
    private void DisplayExaminationMazes()
    {
        GameObject go = new GameObject();
        go.transform.SetParent(environments[0]);
        go.name = "Procedural Maze";
        go.tag = "Wall";
        go.layer = 9;

        go.transform.localPosition = Vector3.zero;

        float x = width;
        float y = environments[0].GetChild(0).localScale.y / 2f;
        float z = width;
        environments[0].GetChild(0).localPosition = new Vector3(x, y, z);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = meshGenerator.FromData(data);
        
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.materials = new Material[2] {mazeMat1, mazeMat2};

        for(int i = 1; i < environments.Length; i++)
        {
            Instantiate(go, environments[i].position, Quaternion.identity).transform.SetParent(environments[i]);
            environments[i].GetChild(0).localPosition = new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// Destroys the specified maze
    /// </summary>
    /// <param name="environment"></param>
    public void DisposeSingleOldMaze(int environment)
    {
        if(environment >= environments.Length || environment < 0) return;
        for(int i = 3; i < environments[environment].childCount; i++)
        {
            Destroy(environments[environment].GetChild(i).gameObject);
        }
    }

    public void DisposeAllOldMazes()
    {
        for(int i = 0; i < environments.Length; i++)
        {
            DisposeSingleOldMaze(i);
        }
    }

    /// <summary>
    /// Places the agents start position into the given maze
    /// </summary>
    /// <param name="location"></param>
    private void PlaceStartTrigger(Transform location = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if(location != null) 
        {
            go.transform.SetParent(location);
            location.GetComponentInChildren<MazeAgent>().startPos = go.transform;
        }
        go.transform.localPosition = new Vector3(width, .5f, width);
        go.name = "Start Trigger";
        go.tag = "Untagged";
        go.layer = 8;

        go.GetComponent<BoxCollider>().isTrigger = true;
        go.GetComponent<MeshRenderer>().sharedMaterial = startMat;
        go.SetActive(false);
    }

    /// <summary>
    /// Places the goal into the given maze
    /// </summary>
    /// <param name="location"></param>
    private void PlaceGoalTrigger(Transform location = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(3, 3, 3);
        if(location != null) 
        {
            go.transform.SetParent(location);
            location.GetComponentInChildren<MazeAgent>().goals.Add(go.transform);
            go.transform.SetParent(location.GetChild(2));
            go.transform.localPosition = RandomizeGoalPlacement(location);
        }
        go.name = "Treasure";
        go.tag = "Goal";
        go.layer = 8;

        go.GetComponent<BoxCollider>().isTrigger = true;
        go.GetComponent<MeshRenderer>().sharedMaterial = treasureMat;
    }

    /// <summary>
    /// Destroys all of the goals and creates them all again
    /// </summary>
    /// <param name="environment"></param>
    public void ResetGoal(int environment)
    {
        environments[environment].GetComponentInChildren<MazeAgent>().goals.Clear();
        for(int i = 0; i < environments[environment].GetChild(2).childCount; i++)
        {
            Destroy(environments[environment].GetChild(2).GetChild(i).gameObject);
        }

        for(int i = 0; i < goalCount; i++)
        {
            PlaceGoalTrigger(environments[environment]);
        }
    }

    /// <summary>
    /// Destroys a specific goal and then creates it again
    /// </summary>
    /// <param name="data"></param>
    public void ResetGoal(System.Tuple<int, int> data)
    {
        Destroy(environments[data.Item1].GetChild(2).GetChild(data.Item2).gameObject);
        PlaceGoalTrigger(environments[data.Item1]);
    }

    /// <summary>
    /// If randomizeGoals is true returns a randomised position for the goal else returns the top right position of the maze
    /// </summary>
    /// <param name="environment"></param>
    /// <returns></returns>
    public Vector3 RandomizeGoalPlacement(Transform environment)
    {
        if(randomizeGoals)
        {
            List<Vector3> goalLocations = CollectPossibleGoalLocations(environment.position, environment.GetSiblingIndex());
            // If nothing reachable turned up (a heavily walled-off maze), fall back to the
            // start cell itself - guaranteed open and reachable, unlike the fixed corner
            // used below, which isn't reachability-checked at all.
            if(goalLocations.Count == 0)
            {
                Debug.LogWarning($"No reachable goal candidates for environment {environment.GetSiblingIndex()}; falling back to start cell.");
                return new Vector3(width, 1.5f, width);
            }
            return goalLocations[Random.Range(0, goalLocations.Count)];
        }
        else return new Vector3(width*rows, 1.5f, width*(cols-4));

    }

    /// <summary>
    /// Walks each possible "square" of the maze and collects the positions which are reachable
    /// from the start cell, reading straight from that environment's own maze data instead of
    /// raycasting against the rendered mesh.
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    private List<Vector3> CollectPossibleGoalLocations(Vector3 origin, int environment)
    {
        // Read the size straight off this environment's own maze array rather than
        // GameController's current sizeRows/sizeCols - those are shared, global curriculum
        // values that can advance ahead of this specific environment's next regeneration,
        // which previously let this loop search past the edge of the maze actually on screen.
        int[,] maze = environmentMazeData[environment];
        int sizeRows = maze.GetUpperBound(0);
        int sizeCols = maze.GetUpperBound(1);

        bool[,] reachable = GetReachableCells(maze, sizeRows, sizeCols);

        List<Vector3> goalLocations = new List<Vector3>();

        for(int i = 1; i <= sizeRows; i++)
        {
            origin.z += width;
            for(int j = 1; j <= sizeCols; j++)
            {
                origin.x += width;
                if(reachable[i, j])
                {
                    goalLocations.Add(new Vector3(origin.x, 1.5f, origin.z));
                }
            }
            origin.x -= width*sizeCols;
        }

        return goalLocations;
    }

    /// <summary>
    /// Flood-fills outward from the fixed start cell (1,1) over the given maze to find every cell
    /// that is actually walkable from the start, since the wall generator gives no such guarantee
    /// on its own.
    /// </summary>
    /// <param name="maze"></param>
    /// <param name="sizeRows"></param>
    /// <param name="sizeCols"></param>
    /// <returns>A grid, sized to match the goal-search loop, where true means reachable from start.</returns>
    private bool[,] GetReachableCells(int[,] maze, int sizeRows, int sizeCols)
    {
        bool[,] visited = new bool[sizeRows + 1, sizeCols + 1];
        int rMax = maze.GetUpperBound(0);
        int cMax = maze.GetUpperBound(1);

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(new Vector2Int(1, 1));
        visited[1, 1] = true;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while(frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();
            foreach(Vector2Int dir in directions)
            {
                int ni = cell.x + dir.x;
                int nj = cell.y + dir.y;

                if(ni < 0 || ni > rMax || nj < 0 || nj > cMax) continue;
                if(visited[ni, nj] || maze[ni, nj] == 1) continue;

                visited[ni, nj] = true;
                frontier.Enqueue(new Vector2Int(ni, nj));
            }
        }

        return visited;
    }
    
    void OnGUI()
    {
        if (!showDebug)
        {
            return;
        }

        int[,] maze = data;
        int rMax = maze.GetUpperBound(0);
        int cMax = maze.GetUpperBound(1);

        string msg = "";

        for (int i = rMax; i >= 0; i--)
        {
            for (int j = 0; j <= cMax; j++)
            {
                if (maze[i, j] == 0)
                {
                    msg += "....";
                }
                else
                {
                    msg += "==";
                }
            }
            msg += "\n";
        }

        GUI.Label(new Rect(20, 20, 500, 500), msg);
    }
}