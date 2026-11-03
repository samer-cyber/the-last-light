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
    /// What the scene builders share: materials, textures and meshes made or updated in place, objects,
    /// sounds found by name, wiring of serialized fields, render passes, panels and the held notebook.
    /// </summary>
    public static class SceneKit
    {
        public const string Root = "Assets/Game";
        public const string Models = Root + "/Models/Character";

        // ---- Objects ----

        public static Renderer MakePart(string name, Transform parent, Mesh mesh, Material material, float scale)
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

        public static ParticleSystem MakeParticles(string name, Transform parent, Material material)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var particles = holder.AddComponent<ParticleSystem>();
            holder.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }

        public static AudioSource MakeSource(string name, Transform parent, AudioClip clip, bool loop)
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

        public static AudioClip FindClip(string name) =>
            AssetDatabase.FindAssets($"{name} t:AudioClip", new[] { Root + "/Audio" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(clip => clip != null && clip.name == name);

        /// <summary>
        /// The clip played backwards, saved as a WAV beside it: an explosion reversed swells from
        /// nothing to its loudest at the very end, which is the sound of the orb gathering itself.
        /// </summary>
        public static AudioClip MakeReversed(AudioClip source, string name)
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

        /// <summary>
        /// The notebook and pencil held in front of the camera, with materials for a dark room lit only
        /// by the orb, and the page showing the panel's texture.
        /// </summary>
        public static HeldNotebook MakeHeldNotebook(Transform camera, Orb orb, PanelSettings pagePanel)
        {
            var held = new GameObject("Held").AddComponent<HeldNotebook>();
            held.transform.SetParent(camera, false);
            var notebook = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Models}/Notebook.fbx"));
            notebook.transform.SetParent(held.transform, false);
            var pencil = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Models}/Pencil.fbx"));
            pencil.transform.SetParent(held.transform, false);

            var looks = new Dictionary<string, Material>
            {
                ["NotebookCover"] = MakeLit("Notebook Cover", new Color(0.09f, 0.11f, 0.2f), 0.35f, 0f),
                ["Paper"] = MakeLit("Notebook Paper Edge", new Color(0.86f, 0.84f, 0.78f), 0.15f, 0f),
                ["Wire"] = MakeLit("Wire", new Color(0.62f, 0.62f, 0.66f), 0.75f, 1f),
                ["PencilPaint"] = MakeLit("Pencil Paint", new Color(0.93f, 0.68f, 0.12f), 0.55f, 0f),
                ["PencilWood"] = MakeLit("Pencil Wood", new Color(0.86f, 0.66f, 0.45f), 0.25f, 0f),
                ["PencilGraphite"] = MakeLit("Pencil Graphite", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.3f),
                ["Eraser"] = MakeLit("Eraser", new Color(0.88f, 0.52f, 0.55f), 0.1f, 0f),
            };
            var pageMaterial = MakeMaterial("Notebook Page", "Game/Page", m => m.SetTexture("_MainTex", pagePanel.targetTexture));
            Renderer page = null;
            foreach (var renderer in notebook.GetComponentsInChildren<Renderer>().Concat(pencil.GetComponentsInChildren<Renderer>()))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer.name == "Page")
                {
                    page = renderer;
                    renderer.sharedMaterial = pageMaterial;
                    continue;
                }
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(m => m != null && looks.TryGetValue(m.name, out var look) ? look : m).ToArray();
            }
            if (page == null) throw new InvalidOperationException("Notebook.fbx has no object named Page");
            Wire(held, ("notebook", notebook.transform), ("page", page), ("pencil", pencil.transform), ("orb", orb));
            return held;
        }

        public static Material MakeLit(string name, Color colour, float smoothness, float metallic) =>
            MakeMaterial(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", colour);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
            });

        /// <summary>A panel that draws into a render texture shaped like an A6 page, 640 by 902 units.</summary>
        public static PanelSettings MakeNotebookPanel(ThemeStyleSheet theme)
        {
            string texturePath = $"{Root}/UI/Notebook Page.renderTexture";
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (texture == null)
            {
                texture = new RenderTexture(1024, 1444, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Notebook Page",
                    useMipMap = true,
                    autoGenerateMips = true,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
                };
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            string path = $"{Root}/UI/Notebook Panel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = theme;
            panel.targetTexture = texture;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(640, 902);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0f;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0.96f, 0.94f, 0.89f);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        public static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a private serialized number or switch by name.</summary>
        public static void SetValue(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            if (value is bool flag) property.boolValue = flag;
            else if (value is Vector3 vector) property.vector3Value = vector;
            else if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Convert.ToInt32(value);
            else property.floatValue = Convert.ToSingle(value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets private serialized fields by name.</summary>
        public static void Wire(Object target, params (string field, Object value)[] links)
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

        public static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Could not load {path}");

        public static Material MakeMaterial(string name, string shaderName, Action<Material> setUp = null)
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

        /// <summary>
        /// Sets up the PC renderer: the full-screen warp, then the orb's own light (its layer, left out
        /// of the ordinary transparent pass) drawn on top of the bent image. Safe to run again.
        /// </summary>
        public static void AddRenderPasses(Material warpMaterial, int orbLightLayer)
        {
            const string rendererPath = "Assets/Settings/PC_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath) ?? throw new InvalidOperationException($"No renderer at {rendererPath}");

            var warp = AddFeature<FullScreenPassRendererFeature>(renderer, "Warp");
            warp.passMaterial = warpMaterial;
            warp.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            warp.requirements = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            warp.fetchColorBuffer = true;
            warp.SetActive(true);

            var light = AddFeature<RenderObjects>(renderer, "Orb Light");
            light.settings.Event = RenderPassEvent.BeforeRenderingPostProcessing;
            light.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            light.settings.filterSettings.LayerMask = 1 << orbLightLayer;
            light.SetActive(true);

            renderer.transparentLayerMask = ~(1 << orbLightLayer);
            EditorUtility.SetDirty(warp);
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The renderer's feature of that type and name, added at the end of its list if missing.</summary>
        public static T AddFeature<T>(UniversalRendererData renderer, string name) where T : ScriptableRendererFeature
        {
            var feature = renderer.rendererFeatures.OfType<T>().FirstOrDefault(f => f.name == name);
            if (feature != null) return feature;
            feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            // The renderer keeps a list of its features and a parallel list of their file ids.
            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return feature;
        }

        /// <summary>The index of a named layer, added in the first free user slot if missing.</summary>
        public static int EnsureLayer(string name)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                tags.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("No free layer for " + name);
        }

        public static Material MakeParticleMaterial(string name, Texture2D texture, float intensity) =>
            MakeMaterial(name, "Game/AdditiveParticle", m =>
            {
                m.SetTexture("_MainTex", texture);
                m.SetFloat("_Intensity", intensity);
            });

        public static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        public static PanelSettings MakePanel()
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

        public static Texture2D MakeTexture(string name, int size, bool alpha, Func<float, float, Color> pixel)
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

        public static Color SoftDot(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float floor = Mathf.Exp(-4f);
            float a = Mathf.Clamp01((Mathf.Exp(-r * r * 4f) - floor) / (1f - floor));
            return new Color(1f, 1f, 1f, a);
        }

        /// <summary>A thin bright ring at 0.86 of the radius, with a faint trail inside it.</summary>
        public static Color Ring(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float edge = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.024f, 2f));
            float trail = r < 0.86f ? 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.1f, 2f)) : 0f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(edge + trail));
        }

        /// <summary>Warm off-white paper with faint fibres and blotches.</summary>
        public static Color Paper(float u, float v)
        {
            float fibres = 0.5f * Mathf.PerlinNoise(u * 180f, v * 14f) + 0.5f * Mathf.PerlinNoise(u * 22f + 3.1f, v * 160f);
            float blotches = Mathf.PerlinNoise(u * 5f + 11.3f, v * 5f + 7.7f);
            float grain = Mathf.PerlinNoise(u * 600f + 1.7f, v * 600f + 9.1f);
            float shade = 0.955f + 0.03f * (fibres - 0.5f) + 0.035f * (blotches - 0.5f) + 0.015f * (grain - 0.5f);
            return new Color(shade, shade * 0.976f, shade * 0.925f, 1f);
        }

        /// <summary>A smooth sphere of radius 0.5; Unity's own is too coarse at the orb's size.</summary>
        public static Mesh MakeSphere(int segments, int rings)
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
        public static Mesh MakeBillboard() =>
            SaveMesh("Billboard", mesh =>
            {
                mesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
                mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
                // It is turned in the shader, so its bounds must hold it at any angle.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            });

        public static Mesh SaveMesh(string name, Action<Mesh> fill)
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
