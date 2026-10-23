using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowBlade.EditorTools
{
    /// <summary>
    /// Compilation du jeu en ligne de commande (utilisée pour les tests automatiques).
    /// Exemple : Unity.exe -batchmode -quit -projectPath ... -executeMethod ShadowBlade.EditorTools.PlayerBuilds.BuildWindows -buildPath C:\Build\ShadowBlade.exe
    /// </summary>
    public static class PlayerBuilds
    {
        /// <summary>Reconstruit le menu et l'arène, puis compile la version Windows.</summary>
        public static void RebuildScenesAndBuildWindows()
        {
            MainMenuBuilder.BuildFromCommandLine();
            BuildWindows();
        }

        public static void BuildWindows()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-buildPath");
            string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Builds/Windows/ShadowBlade.exe";

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(scenes, path, BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log($"BUILD_RESULT {report.summary.result} {report.summary.totalErrors} erreur(s) -> {path}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
