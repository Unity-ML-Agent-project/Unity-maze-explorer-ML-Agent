using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line Linux player build for the shared training server. Example:
/// Unity.exe -batchmode -projectPath &lt;project&gt; -buildTarget Linux64 -executeMethod BuildLinuxPlayers.Build
///     -buildScene Assets/Scenes/MazeRunnerVector.unity -buildOutput Builds/Vector/MazeVector.x86_64
/// Builds only the given scene, as a normal (non-development, non-dedicated-server) player.
/// </summary>
public static class BuildLinuxPlayers
{
    public static void Build()
    {
        string scene = GetArg("-buildScene");
        string output = GetArg("-buildOutput");
        if(string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(output))
        {
            Debug.LogError("BuildLinuxPlayers: -buildScene and -buildOutput are required.");
            EditorApplication.Exit(2);
            return;
        }

        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = output,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"BuildLinuxPlayers: {report.summary.result} ({report.summary.totalSize / (1024 * 1024)} MB) -> {output}");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for(int i = 0; i < args.Length - 1; i++)
        {
            if(args[i] == name) return args[i + 1];
        }
        return null;
    }
}
