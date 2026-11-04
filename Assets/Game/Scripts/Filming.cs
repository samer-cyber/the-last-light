using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// Films the game for the trailer. While rolling, game time steps exactly 1/fps each frame whatever the real
    /// speed (so waits on the network shrink to almost nothing in the film); the camera and the HUD are drawn at
    /// 1920×1080 into textures of their own, laid one over the other and written as numbered JPEGs; the game's
    /// sound is mixed offline (<see cref="AudioRenderer"/>) into a WAV beside them, in step with the frames.
    /// tools/assemble_film.py turns a folder into an MP4. Only the editor's test helpers start it.
    /// </summary>
    public class Filming : MonoBehaviour
    {
        public static Filming Current { get; private set; }
        public string Folder { get; private set; }
        public int Frames { get; private set; }

        Camera view;
        RenderTexture world, overlay;
        PanelSettings[] panels;
        (bool, Color)[] clears;
        Texture2D worldFrame, overlayFrame;
        Color32[] mixed;
        FileStream sound;
        int channels, quality = 92;
        long sampleBytes;
        int width, height;

        public static Filming Begin(string folder, int fps = 30, int width = 1920, int height = 1080)
        {
            if (Current) Current.End();
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.jpg")) File.Delete(old);
            var go = new GameObject("Filming");
            var f = go.AddComponent<Filming>();
            f.Folder = folder;
            f.width = width;
            f.height = height;
            f.view = Camera.main;
            f.world = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Film World", antiAliasing = 1 };
            f.overlay = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Film Overlay" };
            f.view.targetTexture = f.world;
            f.panels = FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Select(d => d.panelSettings).Where(p => p).Distinct().ToArray();
            f.clears = f.panels.Select(p => (p.clearColor, p.colorClearValue)).ToArray();
            foreach (var p in f.panels)
            {
                p.targetTexture = f.overlay;
                // Cleared every frame, or what moved leaves a trail.
                p.clearColor = true;
                p.colorClearValue = Color.clear;
            }
            f.worldFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
            f.overlayFrame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            f.mixed = new Color32[width * height];
            f.channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            f.sound = new FileStream(Path.Combine(folder, "sound.wav"), FileMode.Create, FileAccess.Write);
            f.sound.Write(new byte[44], 0, 44);
            Time.captureFramerate = fps;
            AudioRenderer.Start();
            Current = f;
            return f;
        }

        void LateUpdate()
        {
            // Sound for this frame, mixed offline.
            int n = AudioRenderer.GetSampleCountForCaptureFrame();
            if (n > 0)
            {
                var buffer = new NativeArray<float>(n * channels, Allocator.Temp);
                AudioRenderer.Render(buffer);
                var bytes = new byte[buffer.Length * 2];
                for (int i = 0; i < buffer.Length; i++)
                {
                    short s = (short)Mathf.Clamp(buffer[i] * 32767f, -32768f, 32767f);
                    bytes[2 * i] = (byte)(s & 0xff);
                    bytes[2 * i + 1] = (byte)((s >> 8) & 0xff);
                }
                buffer.Dispose();
                sound.Write(bytes, 0, bytes.Length);
                sampleBytes += bytes.Length;
            }
            StartCoroutine(Grab());
        }

        System.Collections.IEnumerator Grab()
        {
            yield return new WaitForEndOfFrame();
            if (!world) yield break;
            var previous = RenderTexture.active;
            RenderTexture.active = world;
            worldFrame.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = overlay;
            overlayFrame.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            var under = worldFrame.GetPixels32();
            var over = overlayFrame.GetPixels32();
            // The HUD is drawn premultiplied: its colour already carries its alpha.
            for (int i = 0; i < mixed.Length; i++)
            {
                var o = over[i];
                var u = under[i];
                int keep = 255 - o.a;
                mixed[i] = new Color32((byte)Mathf.Min(255, o.r + u.r * keep / 255), (byte)Mathf.Min(255, o.g + u.g * keep / 255), (byte)Mathf.Min(255, o.b + u.b * keep / 255), 255);
            }
            worldFrame.SetPixels32(mixed);
            File.WriteAllBytes(Path.Combine(Folder, $"{Frames:D6}.jpg"), worldFrame.EncodeToJPG(quality));
            Frames++;
        }

        public void End()
        {
            if (Current != this) return;
            Current = null;
            AudioRenderer.Stop();
            Time.captureFramerate = 0;
            if (view) view.targetTexture = null;
            for (int i = 0; i < panels.Length; i++)
                if (panels[i])
                {
                    panels[i].targetTexture = null;
                    panels[i].clearColor = clears[i].Item1;
                    panels[i].colorClearValue = clears[i].Item2;
                }
            if (sound != null)
            {
                int rate = AudioSettings.outputSampleRate;
                sound.Seek(0, SeekOrigin.Begin);
                using (var w = new BinaryWriter(sound, System.Text.Encoding.ASCII, true))
                {
                    w.Write("RIFF".ToCharArray());
                    w.Write((int)(36 + sampleBytes));
                    w.Write("WAVEfmt ".ToCharArray());
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)channels);
                    w.Write(rate);
                    w.Write(rate * channels * 2);
                    w.Write((short)(channels * 2));
                    w.Write((short)16);
                    w.Write("data".ToCharArray());
                    w.Write((int)sampleBytes);
                }
                sound.Dispose();
                sound = null;
            }
            if (world) world.Release();
            if (overlay) overlay.Release();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) End();
        }
    }
}
