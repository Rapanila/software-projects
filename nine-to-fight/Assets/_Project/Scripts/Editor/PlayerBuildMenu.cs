using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NineToFight.EditorTools
{
    public static class PlayerBuildMenu
    {
        public const string WindowsOutput = "Builds/Windows/NineToFight.exe";

        [MenuItem("Nine to Fight/Build Windows Player", priority = 20)]
        public static void BuildWindows()
        {
            if (!File.Exists(StarterContentBuilder.FloorScenePath))
                StarterContentBuilder.BuildAll();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { StarterContentBuilder.FloorScenePath },
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Nine to Fight] Windows build succeeded: {Path.GetFullPath(WindowsOutput)} ({summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(WindowsOutput);
            }
            else
            {
                Debug.LogError($"[Nine to Fight] Windows build {summary.result} with {summary.totalErrors} error(s).");
            }
        }
    }
}
