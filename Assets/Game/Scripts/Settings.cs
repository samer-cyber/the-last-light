using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game
{
    /// <summary>
    /// The player's settings, kept in PlayerPrefs: the picture (quality, full screen), the sound by channel (master,
    /// music, sounds, Spark's voice), the mouse, and how hard the cold and the machines are. And the checkpoint the
    /// game was last saved at.
    /// </summary>
    public static class Settings
    {
        public enum Channel { Music, Sounds, Voice }
        public static readonly string[] Qualities = { "Low", "Medium", "High" };
        public static readonly string[] Difficulties = { "Gentle", "Normal", "Hard" };

        public static int Quality { get => PlayerPrefs.GetInt("quality", 2); set => Set("quality", value); }
        public static bool Fullscreen { get => PlayerPrefs.GetInt("fullscreen", 1) == 1; set => Set("fullscreen", value ? 1 : 0); }
        public static float Master { get => PlayerPrefs.GetFloat("master", 1f); set => Set("master", value); }
        public static float Music { get => PlayerPrefs.GetFloat("music", 0.8f); set => Set("music", value); }
        public static float Sounds { get => PlayerPrefs.GetFloat("sounds", 1f); set => Set("sounds", value); }
        public static float Voice { get => PlayerPrefs.GetFloat("voice", 1f); set => Set("voice", value); }
        public static float Mouse { get => PlayerPrefs.GetFloat("mouse", 1f); set => Set("mouse", value); }
        public static int Difficulty { get => PlayerPrefs.GetInt("difficulty", 1); set => Set("difficulty", value); }

        /// <summary>How much the cold and the machines hurt: half on Gentle, half again on Hard.</summary>
        public static float Harm => Difficulty == 0 ? 0.5f : Difficulty == 2 ? 1.5f : 1f;

        // ---- The checkpoint: 0 none, 1 awake in the vault, 2 outside it ----
        public static int Checkpoint => PlayerPrefs.GetInt("checkpoint", 0);
        public static string SavedName => PlayerPrefs.GetString("survivor", "");
        public static int SavedTrust => PlayerPrefs.GetInt("trust", 8);

        public static void Save(int checkpoint, string survivor, int trust)
        {
            if (checkpoint < Checkpoint) return;
            PlayerPrefs.SetInt("checkpoint", checkpoint);
            if (!string.IsNullOrWhiteSpace(survivor)) PlayerPrefs.SetString("survivor", survivor);
            PlayerPrefs.SetInt("trust", trust);
            PlayerPrefs.Save();
        }

        public static void Forget()
        {
            PlayerPrefs.DeleteKey("checkpoint");
            PlayerPrefs.DeleteKey("survivor");
            PlayerPrefs.DeleteKey("trust");
            PlayerPrefs.Save();
        }

        static readonly float[] gains = { 0.8f, 1f, 1f };
        public static float Gain(Channel channel) => gains[(int)channel];

        static void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Apply();
        }

        static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Apply();
        }

        static bool kept;
        static float keptScale, keptShadows;
        static int keptMsaa;

        /// <summary>Makes the settings so: the sound at once, the picture through the render pipeline.</summary>
        public static void Apply()
        {
            gains[0] = Music;
            gains[1] = Sounds;
            gains[2] = Voice;
            AudioListener.volume = Master;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (!kept)
                {
                    kept = true;
                    keptScale = urp.renderScale;
                    keptShadows = urp.shadowDistance;
                    keptMsaa = urp.msaaSampleCount;
                }
                int q = Quality;
                urp.renderScale = keptScale * (q == 0 ? 0.7f : q == 1 ? 0.85f : 1f);
                urp.shadowDistance = keptShadows * (q == 0 ? 0.4f : q == 1 ? 0.7f : 1f);
                urp.msaaSampleCount = q == 0 ? 1 : keptMsaa;
            }
#if !UNITY_EDITOR
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
#endif
        }

        /// <summary>Puts the pipeline's own picture settings back (in the editor the asset outlives play mode).</summary>
        public static void Restore()
        {
            if (!kept || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            urp.renderScale = keptScale;
            urp.shadowDistance = keptShadows;
            urp.msaaSampleCount = keptMsaa;
        }
    }
}
