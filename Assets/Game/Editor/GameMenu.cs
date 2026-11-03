using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Building
{
    /// <summary>
    /// Game > Play the Last Light starts play mode in the game with the Game view maximized; the old
    /// scenes (the Morse opening, the young solar system) can still be played from the menu. Game >
    /// Build for Windows makes a full-screen testing build (debug keys on) of the Last Light alone in
    /// the hackathon folder's build/, next to secrets.env, outside the repository.
    /// </summary>
    public static class GameMenu
    {
        const string Opening = "Assets/Game/Scenes/Opening.unity";
        const string YoungSun = "Assets/Game/Scenes/YoungSun.unity";
        const string LastLight = "Assets/Game/Scenes/LastLight.unity";

        static string pending;

        [MenuItem("Game/Play the Last Light", priority = 0)]
        static void PlayLastLight() => Play(LastLight);

        [MenuItem("Game/Play the Morse Opening (old)", priority = 1)]
        static void PlayOpening() => Play(Opening);

        [MenuItem("Game/Play the Young Solar System (old)", priority = 2)]
        static void PlayYoungSun() => Play(YoungSun);

        static void Play(string scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Leave play mode first; the scene opens once the editor is back in edit mode.
                pending = scene;
                EditorApplication.playModeStateChanged += AfterStop;
                EditorApplication.isPlaying = false;
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scene);
            Maximize();
            EditorApplication.isPlaying = true;
        }

        static void AfterStop(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AfterStop;
            Play(pending);
        }

        // The Game view's "Play Maximized" choice. Unity keeps it on the window and gives it no public API.
        static void Maximize()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var property = type?.GetProperty("enterPlayModeBehavior", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) return;
            var view = EditorWindow.GetWindow(type, false, null, false);
            property.SetValue(view, Enum.Parse(property.PropertyType, "PlayMaximized"));
        }

        [MenuItem("Game/Forget the First Words", priority = 10)]
        static void ForgetFirstWords()
        {
            PlayerPrefs.DeleteKey(MorseEmitter.FirstWordsKey);
            Debug.Log("The opening's message is MAKE ME LIVE again.");
        }

        [MenuItem("Game/Build for Windows", priority = 20)]
        public static string BuildForWindows()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { LastLight },
                locationPathName = Path.Combine(folder, "Game.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            string result = $"{report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalTime.TotalMinutes:F1} min, {folder}";
            Debug.Log("Windows build: " + result);
            return result;
        }
    }
}
