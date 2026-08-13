using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityVerify
{
    public static class VerifyBuild
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        [MenuItem("Unity Verify/Client Mode")]
        public static void SetClientMode()
        {
            PlayerPrefs.SetString("UnityVerifyMode", "client");
        }

        [MenuItem("Unity Verify/Server Mode")]
        public static void SetServerMode()
        {
            PlayerPrefs.SetString("UnityVerifyMode", "server");
        }

        [MenuItem("Unity Verify/Ensure Bootstrap Scene")]
        public static void EnsureBootstrapScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var contains = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath)
                {
                    contains = true;
                    break;
                }
            }

            if (!contains)
            {
                var scenes = new EditorBuildSettingsScene[EditorBuildSettings.scenes.Length + 1];
                for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    scenes[i] = EditorBuildSettings.scenes[i];
                }

                scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = scenes;
            }
        }

        [MenuItem("Unity Verify/Build Windows Server")]
        public static void BuildWindowsServer()
        {
            EnsureBootstrapScene();
            var location = Path.Combine("Builds", "UnityVerifyServer", "UnityVerifyServer.exe");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, location, BuildTarget.StandaloneWindows64, BuildOptions.Development);
            LogResult(report);
        }

        [MenuItem("Unity Verify/Build Windows Client")]
        public static void BuildWindowsClient()
        {
            EnsureBootstrapScene();
            var location = Path.Combine("Builds", "UnityVerifyClient", "UnityVerifyClient.exe");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, location, BuildTarget.StandaloneWindows64, BuildOptions.Development);
            LogResult(report);
        }

        private static void LogResult(BuildReport report)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[UnityVerify] build succeeded: {summary.outputPath}");
            }
            else
            {
                Debug.LogError($"[UnityVerify] build failed: {summary.result} {summary.totalErrors} errors");
            }
        }
    }
}
