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
        const float OrbRadius = 8f;
        const float HaloScale = 3.2f;   // in orb diameters
        const float LensScale = 6f;

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
            var coreMat = MakeMaterial("Orb Core", "Game/OrbCore", m => m.SetFloat("_Intensity", 1.4f));
            var shellMat = MakeMaterial("Orb Shell", "Game/OrbShell", m => m.SetFloat("_Intensity", 1f));
            var haloMat = MakeMaterial("Orb Halo", "Game/Halo", m =>
            {
                m.SetFloat("_Edge", 1f / HaloScale);
                m.SetFloat("_Intensity", 0.45f);
            });
            var lensMat = MakeMaterial("Orb Lens", "Game/Lens");
            var dustMat = MakeParticleMaterial("Dust", softDot, 2.2f);
            var hazeMat = MakeParticleMaterial("Haze", softDot, 1.2f);
            var ringMat = MakeParticleMaterial("Ring", ring, 1.2f);
            var burstMat = MakeParticleMaterial("Burst", softDot, 6f);
            var moteMat = MakeParticleMaterial("Mote", softDot, 0.8f);

            var profile = MakeProfile();
            var panel = MakePanel();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            // The orb.
            var orbObject = new GameObject("Orb");
            var emitter = orbObject.AddComponent<MorseEmitter>();
            var body = new GameObject("Body").transform;
            body.SetParent(orbObject.transform, false);
            body.localScale = Vector3.one * (2f * OrbRadius);
            var core = MakePart("Core", body, sphere, coreMat, 1f);
            var shell = MakePart("Shell", body, sphere, shellMat, 1.035f);
            var halo = MakePart("Halo", body, billboard, haloMat, HaloScale);
            var lens = MakePart("Lens", body, billboard, lensMat, LensScale);
            var dust = MakeParticles("Dust", orbObject.transform, dustMat);
            ParticleRecipes.Dust(dust, OrbRadius);
            var haze = MakeParticles("Haze", orbObject.transform, hazeMat);
            ParticleRecipes.Haze(haze, OrbRadius);
            var rings = MakeParticles("Rings", orbObject.transform, ringMat);
            ParticleRecipes.Rings(rings);
            var burst = MakeParticles("Burst", orbObject.transform, burstMat);
            ParticleRecipes.Burst(burst, OrbRadius);
            var orb = orbObject.AddComponent<Orb>();
            Wire(orb, ("emitter", emitter), ("body", body), ("core", core), ("shell", shell), ("halo", halo), ("lens", lens),
                ("dust", dust), ("haze", haze), ("rings", rings), ("burst", burst));

            // The player, a little above the orb's equator, facing it.
            var playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0f, 3f, -42f);
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

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            // Sound.
            var sound = new GameObject("Sound").transform;
            var drone = MakeSource("Drone", sound, FindClip("void_drone"), true);
            var effects = MakeSource("Effects", sound, null, false);
            var voice = MakeSource("Voice", sound, null, true).gameObject.AddComponent<MorseVoice>();
            Wire(voice, ("emitter", emitter));
            var paperSound = MakeSource("Paper", sound, null, false);

            // What is drawn over the world. The style sheets are loaded only now: opening the new
            // scene unloads assets nothing refers to yet, and a reference taken before it goes null.
            var notepadStyle = Load<StyleSheet>($"{Root}/UI/Notepad.uss");
            var hudStyle = Load<StyleSheet>($"{Root}/UI/Hud.uss");
            var interfaceObject = new GameObject("Interface");
            var document = interfaceObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var notepad = interfaceObject.AddComponent<Notepad>();
            Wire(notepad, ("document", document), ("style", notepadStyle), ("paper", paper), ("emitter", emitter), ("player", player),
                ("sound", paperSound), ("openClip", FindClip("paper_open")), ("closeClip", FindClip("paper_close")), ("writeClip", FindClip("pencil_write")));
            var hud = interfaceObject.AddComponent<Hud>();
            Wire(hud, ("document", document), ("style", hudStyle));

            var breakClip = FindClip("orb_break");
            var director = new GameObject("Director").AddComponent<OpeningDirector>();
            Wire(director, ("player", player), ("orb", orb), ("emitter", emitter), ("voice", voice), ("notepad", notepad), ("hud", hud),
                ("volume", volume), ("drone", drone), ("effects", effects), ("risingClip", FindClip("orb_rising")), ("breakClip", breakClip),
                ("gatherClip", MakeReversed(breakClip, "orb_gather")));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}");
        }

        // ---- Objects ----

        static Renderer MakePart(string name, Transform parent, Mesh mesh, Material material, float scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localScale = Vector3.one * scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        static ParticleSystem MakeParticles(string name, Transform parent, Material material)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var particles = holder.AddComponent<ParticleSystem>();
            holder.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }

        static AudioSource MakeSource(string name, Transform parent, AudioClip clip, bool loop)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        static AudioClip FindClip(string name) =>
            AssetDatabase.FindAssets($"{name} t:AudioClip", new[] { Root + "/Audio" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(clip => clip != null && clip.name == name);

        /// <summary>
        /// The clip played backwards, saved as a WAV beside it: an explosion reversed swells from
        /// nothing to its loudest at the very end, which is the sound of the orb gathering itself.
        /// </summary>
        static AudioClip MakeReversed(AudioClip source, string name)
        {
            if (source == null) return null;
            source.LoadAudioData();
            int channels = source.channels, frames = source.samples;
            var data = new float[frames * channels];
            source.GetData(data, 0);
            var reversed = new float[data.Length];
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < channels; c++)
                    reversed[f * channels + c] = data[(frames - 1 - f) * channels + c];

            string path = $"{Root}/Audio/{name}.wav";
            using (var file = new BinaryWriter(File.Create(path)))
            {
                int bytes = reversed.Length * 2;
                file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                file.Write(36 + bytes);
                file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                file.Write(16);
                file.Write((short)1);
                file.Write((short)channels);
                file.Write(source.frequency);
                file.Write(source.frequency * channels * 2);
                file.Write((short)(channels * 2));
                file.Write((short)16);
                file.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                file.Write(bytes);
                foreach (float sample in reversed) file.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Sets private serialized fields by name.</summary>
        static void Wire(Object target, params (string field, Object value)[] links)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in links)
            {
                var property = serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Assets ----

        static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Could not load {path}");

        static Material MakeMaterial(string name, string shaderName, Action<Material> setUp = null)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException($"Shader not found: {shaderName}");
            string path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            setUp?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MakeParticleMaterial(string name, Texture2D texture, float intensity) =>
            MakeMaterial(name, "Game/AdditiveParticle", m =>
            {
                m.SetTexture("_MainTex", texture);
                m.SetFloat("_Intensity", intensity);
            });

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

        static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static PanelSettings MakePanel()
        {
            string themePath = $"{Root}/UI/Game Theme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            string path = $"{Root}/UI/Game Panel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static Texture2D MakeTexture(string name, int size, bool alpha, Func<float, float, Color> pixel)
        {
            string path = $"{Root}/Textures/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color SoftDot(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float floor = Mathf.Exp(-4f);
            float a = Mathf.Clamp01((Mathf.Exp(-r * r * 4f) - floor) / (1f - floor));
            return new Color(1f, 1f, 1f, a);
        }

        /// <summary>A thin bright ring at 0.86 of the radius, with a faint trail inside it.</summary>
        static Color Ring(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float edge = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.024f, 2f));
            float trail = r < 0.86f ? 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.1f, 2f)) : 0f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(edge + trail));
        }

        /// <summary>Warm off-white paper with faint fibres and blotches.</summary>
        static Color Paper(float u, float v)
        {
            float fibres = 0.5f * Mathf.PerlinNoise(u * 180f, v * 14f) + 0.5f * Mathf.PerlinNoise(u * 22f + 3.1f, v * 160f);
            float blotches = Mathf.PerlinNoise(u * 5f + 11.3f, v * 5f + 7.7f);
            float grain = Mathf.PerlinNoise(u * 600f + 1.7f, v * 600f + 9.1f);
            float shade = 0.955f + 0.03f * (fibres - 0.5f) + 0.035f * (blotches - 0.5f) + 0.015f * (grain - 0.5f);
            return new Color(shade, shade * 0.976f, shade * 0.925f, 1f);
        }

        /// <summary>A smooth sphere of radius 0.5; Unity's own is too coarse at the orb's size.</summary>
        static Mesh MakeSphere(int segments, int rings)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float theta = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)s / segments, 1f - (float)r / rings));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }
            return SaveMesh("Sphere", mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
            });
        }

        /// <summary>A unit quad for shaders that turn it to face the camera.</summary>
        static Mesh MakeBillboard() =>
            SaveMesh("Billboard", mesh =>
            {
                mesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
                mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
                // It is turned in the shader, so its bounds must hold it at any angle.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            });

        static Mesh SaveMesh(string name, Action<Mesh> fill)
        {
            string path = $"{Root}/Meshes/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            else mesh.Clear();
            fill(mesh);
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
