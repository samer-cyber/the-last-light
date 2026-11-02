using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static Game.Building.SceneKit;

namespace Game.Building
{
    /// <summary>
    /// Builds the opening scene from nothing: textures, meshes, materials, the post-processing
    /// profile and every object, wired together. Running it again replaces the scene; materials
    /// and other assets are updated in place so that references to them hold.
    /// </summary>
    public static class OpeningSceneBuilder
    {
        const string Root = "Assets/Game";
        const string ScenePath = Root + "/Scenes/Opening.unity";
        const float CoreRadius = 0.4f;   // at the start; the orb grows slowly
        const float CoronaScale = 12f;   // billboard sizes, in core diameters
        const float GlowScale = 6f;
        const float DiskCentre = 6.5f;   // the dust disc: centre line and half-width, metres
        const float DiskWidth = 4.5f;
        const float HazeRadius = 7f;

        [MenuItem("Game/Build Opening Scene")]
        public static void Build()
        {
            foreach (string folder in new[] { "Scenes", "Materials", "Textures", "Meshes", "Settings", "UI", "Audio" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{folder}")) AssetDatabase.CreateFolder(Root, folder);

            var softDot = MakeTexture("Soft Dot", 64, true, SoftDot);
            var ring = MakeTexture("Ring", 256, true, Ring);
            var paper = MakeTexture("Paper", 1024, false, Paper);
            var sphere = MakeSphere(160, 96);
            var billboard = MakeBillboard();

            var sky = MakeMaterial("Void Sky", "Game/VoidSky");
            foreach (string old in new[] { "Materials/Orb Shell.mat", "Materials/Orb Lens.mat", "Materials/Orb Halo.mat", "Shaders/OrbShell.shader", "Shaders/Lens.shader" })
                AssetDatabase.DeleteAsset($"{Root}/{old}");
            var coreMat = MakeMaterial("Orb Core", "Game/OrbCore", m =>
            {
                m.SetColor("_Deep", new Color(0.22f, 0.16f, 0.75f));
                m.SetColor("_Mid", new Color(0.5f, 0.42f, 1f));
                m.SetColor("_Hot", new Color(0.72f, 0.92f, 1f));
                m.SetColor("_Core", Color.white);
                m.SetFloat("_Intensity", 6f);
                m.SetFloat("_Scale", 1.6f);
                m.SetFloat("_Flow", 0.12f);
                m.SetFloat("_Swirl", 3f);
            });
            var coronaMat = MakeMaterial("Orb Corona", "Game/Corona", m =>
            {
                m.SetFloat("_Edge", 1f / CoronaScale);
                m.SetFloat("_Intensity", 1.6f);
            });
            var glowMat = MakeMaterial("Orb Glow", "Game/Halo", m =>
            {
                m.SetFloat("_Edge", 1f / GlowScale);
                m.SetFloat("_Intensity", 1.2f);
                m.SetColor("_Color", new Color(0.55f, 0.5f, 1f));
            });
            int orbLight = EnsureLayer("OrbLight");
            AddRenderPasses(MakeMaterial("Warp", "Game/Warp"), orbLight);
            var dustMat = MakeParticleMaterial("Dust", softDot, 2.2f);
            var hazeMat = MakeParticleMaterial("Haze", softDot, 1.2f);
            var ringMat = MakeParticleMaterial("Ring", ring, 0.6f);
            var burstMat = MakeParticleMaterial("Burst", softDot, 6f);
            var moteMat = MakeParticleMaterial("Mote", softDot, 0.8f);

            var profile = MakeProfile();
            var panel = MakePanel();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            // A faint violet fill, so the hands and the notebook are not pitch black on their dark side.
            RenderSettings.ambientLight = new Color(0.05f, 0.045f, 0.08f);
            RenderSettings.fog = false;

            // The orb.
            var orbObject = new GameObject("Orb");
            var emitter = orbObject.AddComponent<MorseEmitter>();
            var body = new GameObject("Body").transform;
            body.SetParent(orbObject.transform, false);
            body.localScale = Vector3.one * (2f * CoreRadius);
            var core = MakePart("Core", body, sphere, coreMat, 1f);
            var corona = MakePart("Corona", body, billboard, coronaMat, CoronaScale);
            var glow = MakePart("Glow", body, billboard, glowMat, GlowScale);
            var dust = MakeParticles("Dust", orbObject.transform, dustMat);
            ParticleRecipes.Dust(dust, DiskCentre, DiskWidth);
            var haze = MakeParticles("Haze", orbObject.transform, hazeMat);
            ParticleRecipes.Haze(haze, HazeRadius);
            var rings = MakeParticles("Rings", orbObject.transform, ringMat);
            ParticleRecipes.Rings(rings);
            var burst = MakeParticles("Burst", orbObject.transform, burstMat);
            ParticleRecipes.Burst(burst, CoreRadius);
            // The orb's own light is drawn after the warp (see AddRenderPasses), so it does not bend into itself.
            foreach (var lit in new Component[] { corona, glow, rings }) lit.gameObject.layer = orbLight;
            var warp = orbObject.AddComponent<SpaceWarp>();
            var shine = new GameObject("Shine").AddComponent<Light>();
            shine.transform.SetParent(orbObject.transform, false);
            shine.type = LightType.Directional;
            shine.color = new Color(0.78f, 0.74f, 1f);
            shine.shadows = LightShadows.None;
            var orb = orbObject.AddComponent<Orb>();
            Wire(orb, ("emitter", emitter), ("warp", warp), ("body", body), ("core", core), ("corona", corona), ("glow", glow),
                ("dust", dust), ("haze", haze), ("rings", rings), ("burst", burst), ("shine", shine));

            // The player, a little above the dust disc, facing the orb.
            var playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0f, 3.5f, -30f);
            Vector3 toOrb = orbObject.transform.position - playerObject.transform.position;
            playerObject.transform.rotation = Quaternion.LookRotation(new Vector3(toOrb.x, 0f, toOrb.z));
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localRotation = Quaternion.Euler(-Mathf.Asin(toOrb.normalized.y) * Mathf.Rad2Deg, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 4000f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderShadows = false;
            cameraData.dithering = true;
            cameraData.stopNaN = true;
            cameraObject.AddComponent<AudioListener>();
            var motes = MakeParticles("Motes", cameraObject.transform, moteMat);
            ParticleRecipes.Motes(motes);
            var player = playerObject.AddComponent<FloatingPlayer>();
            Wire(player, ("view", camera), ("orb", orbObject.transform));
            Wire(warp, ("view", camera));
            Wire(orb, ("viewer", camera.transform));

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            // Sound.
            // Newer sounds (Audio/v2) are used when present, the first ones otherwise.
            var sound = new GameObject("Sound").transform;
            var droneV2 = FindClip("void_drone_v2");
            var drone = MakeSource("Drone", sound, droneV2 ?? FindClip("void_drone"), true);
            var near = MakeSource("Near", sound, FindClip("near_orb_loop"), true);
            var music = MakeSource("Music", sound, FindClip("opening_1") ?? FindClip("music_void"), true);
            var effects = MakeSource("Effects", sound, null, false);
            var pulse = new GameObject("Pulse").AddComponent<MorsePulse>();
            pulse.transform.SetParent(sound, false);
            // Pulse H (a heartbeat through steel) since 2 Nov; G and I are on sound-board/v3.html.
            Wire(pulse, ("emitter", emitter), ("dot", FindClip("pulse_dot_H") ?? FindClip("pulse_dot")), ("dash", FindClip("pulse_dash_H") ?? FindClip("pulse_dash")));
            var voice = MakeSource("Voice", sound, null, true).gameObject.AddComponent<MorseVoice>();
            Wire(voice, ("emitter", emitter));
            SetValue(voice, "followMessage", !pulse.HasClips);
            var paperSound = MakeSource("Paper", sound, null, false);

            // What is drawn over the world. The style sheets are loaded only now: opening the new
            // scene unloads assets nothing refers to yet, and a reference taken before it goes null.
            var notepadStyle = Load<StyleSheet>($"{Root}/UI/Notepad.uss");
            var hudStyle = Load<StyleSheet>($"{Root}/UI/Hud.uss");
            var interfaceObject = new GameObject("Interface");
            var document = interfaceObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var hud = interfaceObject.AddComponent<Hud>();
            Wire(hud, ("document", document), ("style", hudStyle));

            // A soft light from the player's side, so their hands and notebook are seen; the orb lights them from behind.
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.transform.SetParent(cameraObject.transform, false);
            fill.transform.localRotation = Quaternion.Euler(28f, 18f, 0f);
            fill.type = LightType.Directional;
            fill.color = new Color(0.92f, 0.9f, 1f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;

            // The notepad comes up on screen, drawn by the same panel as the hints. (MakeHeldNotebook
            // puts a notebook in the player's hands instead; Samer preferred the sheet on screen.)
            var notepad = interfaceObject.AddComponent<Notepad>();
            Wire(notepad, ("document", document), ("style", notepadStyle), ("paper", paper), ("emitter", emitter), ("player", player),
                ("sound", paperSound), ("openClip", FindClip("notebook_raise") ?? FindClip("paper_open")),
                ("closeClip", FindClip("notebook_lower") ?? FindClip("paper_close")), ("writeClip", FindClip("pencil_write")));
            WireArray(notepad, "strokeClips", Enumerable.Range(1, 6).Select(i => (Object)FindClip($"pencil_stroke_{i}")).Where(c => c != null).ToArray());

            var breakClip = FindClip("orb_break_v2") ?? FindClip("orb_break");
            var gatherV2 = FindClip("orb_gather_v2");
            var gatherClip = gatherV2 ?? MakeReversed(FindClip("orb_break"), "orb_gather");
            var director = new GameObject("Director").AddComponent<OpeningDirector>();
            Wire(director, ("player", player), ("orb", orb), ("emitter", emitter), ("voice", voice), ("pulse", pulse), ("notepad", notepad), ("hud", hud),
                ("volume", volume), ("drone", drone), ("nearSound", near), ("music", music), ("effects", effects),
                ("risingClip", gatherV2 ? null : FindClip("orb_rising")), ("breakClip", breakClip), ("gatherClip", gatherClip));
            // The second drone is mixed quiet in the file itself; the first was not.
            if (droneV2) SetValue(director, "droneVolume", 1f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}");
        }

        static VolumeProfile MakeProfile()
        {
            string path = $"{Root}/Settings/Opening Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            AddOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

            var grading = AddOverride<ColorAdjustments>(profile);
            grading.postExposure.Override(0f);
            grading.contrast.Override(8f);
            grading.saturation.Override(6f);

            AddOverride<ChromaticAberration>(profile).intensity.Override(0.12f);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            var grain = AddOverride<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.8f);

            AddOverride<LensDistortion>(profile).intensity.Override(0f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
