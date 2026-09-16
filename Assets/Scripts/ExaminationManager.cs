using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExaminationManager : MonoBehaviour
{
    /// <summary>
    /// Top left = 0, Top right = 1, Bottom left = 2, Bottom right = 3
    /// </summary>
    private Text[] scoreTexts;
    
    /// <summary>
    /// Top left = 0, Top right = 1, Bottom left = 2, Bottom right = 3
    /// </summary>
    private int[] finishedRuns;

    /// <summary>
    /// Top left = 0, Top right = 1, Bottom left = 2, Bottom right = 3
    /// </summary>
    private MazeAgent[] agents;

    /// <summary>
    /// Top left = 0, Top right = 1, Bottom left = 2, Bottom right = 3
    /// </summary>
    private float[] timeScores;

    private float time, timeToEnd = 60;
    private int runIndex = 0;

    [SerializeField]
    private int scoreToEnd = 5, evaluationMaxRunCount = 100;
    [SerializeField]
    private Text timeText;

    void Start()
    {
        GameObject canvas = GameObject.Find("Canvas");

        scoreTexts = canvas.transform.GetComponentsInChildren<Text>();
        agents = transform.GetComponentsInChildren<MazeAgent>();
        timeScores = new float[agents.Length];
        finishedRuns = new int[agents.Length];
    }

    void FixedUpdate()
    {
        if(runIndex >= evaluationMaxRunCount-1) UnityEditor.EditorApplication.isPlaying = false;

        time += Time.fixedDeltaTime;
        timeText.text = ((int)time).ToString();

        for(int i = 0; i < finishedRuns.Length; i++)
        {
            if(finishedRuns[i] == scoreToEnd)
            {
                finishedRuns[i] = -1;
                scoreTexts[i].text = $"Finished!    Time: {time}";
                timeScores[i] = time;
                agents[i].SetPausedActions(true);

            }
        }

        if(CheckIfRunExamFinished()) RestartExamination();
    }

    /// <summary>
    /// Returns true if exam has been running for 60 or longer or finishedRuns array is filled with -1.
    /// </summary>
    /// <returns></returns>
    private bool CheckIfRunExamFinished()
    {
        if(time >= timeToEnd) return true;

        foreach(int score in finishedRuns)
        {
            if(score != -1) return false;
        }

        return true;
    }

    /// <summary>
    /// Restarts the examination and writes the times taken by each agent to a file
    /// </summary>
    public void RestartExamination()
    {
        for(int i = 0; i < timeScores.Length; i++)
        {
            if(!(timeScores[i] > 0))
            {
                timeScores[i] = time;
            }
        }

        time = 0;
        finishedRuns = new int[agents.Length];
        foreach(MazeAgent agent in agents)
        {
            agent.SetPausedActions(false);
            agent.EndEpisode();
        }

        for(int i = 0; i < 4; i++)
        {
            scoreTexts[i].text = "0";
        }

        WriteFinishedRunsToFile();

        timeScores = new float[agents.Length];

        SendMessage("StartNewGame");
    }

    /// <summary>
    /// Increases the finishedRuns number by one
    /// </summary>
    /// <param name="index"></param>
    public void IncreaseScore(int index)
    {
        if(index >= finishedRuns.Length || index < 0) return;
        
        finishedRuns[index] += 1;

        scoreTexts[index].text = finishedRuns[index].ToString();
    }

    /// <summary>
    /// Writes the times to a file. If the file already exists, appends the file
    /// </summary>
    private void WriteFinishedRunsToFile()
    {
        string fileName = "ExaminationTimes.csv";
        StreamWriter sw;
        if(File.Exists(fileName))
        {
            runIndex++;
            sw = File.AppendText(fileName);
            WriteScore(sw);
        }
        else
        {
            using (sw = new StreamWriter(fileName))
            {
                WriteTitle(sw);

                WriteScore(sw);
            }
        }

        sw.Close();
    }

    /// <summary>
    /// Handles the logic for writing the scores to the file
    /// </summary>
    /// <param name="sw"></param>
    private void WriteScore(StreamWriter sw)
    {
        string line = $"{runIndex+1},";
        // Finish times
        for(int i = 0; i < agents.Length; i++)
        {
            line += FloatToStringWithPeriod(timeScores[i]);
            if(i < agents.Length-1)
            {
                line += ",";
            }
        }
        sw.WriteLine(line);
        
    }

    /// <summary>
    /// Makes the float to string with a period instead of comma separating the decimals
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    private string FloatToStringWithPeriod(float number)
    {
        string f = "";

        int whole = (int)number;

        f += whole;

        f += ".";

        f += (number-(float)whole).ToString().Remove(0,2);

        return f;
    }

    /// <summary>
    /// Writes the titles of each field for CSV-file
    /// </summary>
    /// <param name="sw"></param>
    private void WriteTitle(StreamWriter sw)
    {
        
        string line = "RunCount,";
        for(int i = 0; i < agents.Length; i++)
        {
            line += GetLocationString(i);
            if(i < agents.Length-1)
            {
                line += ",";
            }
        }
        sw.WriteLine(line);
    }

    /// <summary>
    /// Takes the index and gives a name for the test environment
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private string GetLocationString(int index)
    {
        switch(index)
        {
            case 0:
                return "Vector";
            case 1:
                return "VectorCurriculum";
            case 2:
                return "Raycast";
            case 3:
                return "RaycastCurriculum";
            default:
                return $"Extra position: {index}";

        }
    }
}
