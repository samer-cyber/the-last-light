using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.Building.SceneKit;

namespace Game.Building
{
    /// <summary>
    /// The downloaded art (all CC0, from Poly Haven and ambientCG; the list is in progress/art-fetched.md,
    /// fetched by tools/fetch_assets.py): surfaces made from texture sets as URP materials at their real
    /// size, blocks whose UVs are in metres so a surface never stretches, and props placed from the glTF
    /// models (imported by glTFast), whole or one piece of a kit.
    /// </summary>
    public static class Art
    {
        public const string Folder = Root + "/Art";

        static readonly Dictionary<string, Mesh> blocks = new Dictionary<string, Mesh>();

        /// <summary>
        /// A surface from a texture set ("ambientCG/Concrete031", "PolyHaven/rusty_metal_03"): its colour,
        /// its normals and a mask packed from its metal, occlusion and roughness, one repeat every
        /// <paramref name="metres"/> on a <see cref="Block"/>.
        /// </summary>
        public static Material Surface(string name, string set, float metres, Color? tint = null, float smoothness = 1f, float bumps = 1f)
        {
            string folder = $"{Folder}/{set}";
            string colour = Find(folder, "_Color.jpg", "_diff_");
            string normal = Find(folder, "_NormalGL.jpg", "_nor_gl_");
            if (colour == null || normal == null) throw new System.InvalidOperationException($"No texture set in {folder}");
            Normal(normal);
            var mask = Mask(folder);
            return MakeMaterial(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>(colour));
                m.SetColor("_BaseColor", tint ?? Color.white);
                m.SetTexture("_BumpMap", Load<Texture2D>(normal));
                m.SetFloat("_BumpScale", bumps);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", smoothness);
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
                m.SetTextureScale("_BaseMap", Vector2.one / metres);
            });
        }

        /// <summary>A box of this size whose UVs are in metres on every face.</summary>
        public static Mesh Block(Vector3 size)
        {
            string name = $"Block {size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
            if (blocks.TryGetValue(name, out var kept) && kept) return kept;
            var mesh = SaveMesh(name, m =>
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var triangles = new List<int>();
                Vector3 h = size * 0.5f;
                // Each face: its normal, and the two directions its UVs run along, right and up as seen from outside.
                void Face(Vector3 n, Vector3 u, Vector3 v, float uLength, float vLength)
                {
                    int first = vertices.Count;
                    Vector3 centre = Vector3.Scale(n, h);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i == 1 || i == 2 ? 1f : -1f, b = i >= 2 ? 1f : -1f;
                        vertices.Add(centre + u * (a * uLength * 0.5f) + v * (b * vLength * 0.5f));
                        normals.Add(n);
                        uvs.Add(new Vector2((a + 1f) * 0.5f * uLength, (b + 1f) * 0.5f * vLength));
                    }
                    triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                }
                Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
                Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
                Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
                Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
                Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
                Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
                m.SetVertices(vertices);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.SetTriangles(triangles, 0);
                m.RecalculateTangents();
                m.RecalculateBounds();
            });
            blocks[name] = mesh;
            return mesh;
        }

        /// <summary>A block of a surface, placed in its parent: solid unless said otherwise, casting shadows.</summary>
        public static Renderer Slab(string name, Transform parent, Material material, Vector3 at, Vector3 size, bool solid = true, Quaternion? turn = null)
        {
            var part = MakePart(name, parent, Block(size), material, 1f);
            part.transform.localPosition = at;
            part.transform.localRotation = turn ?? Quaternion.identity;
            part.shadowCastingMode = ShadowCastingMode.On;
            part.receiveShadows = true;
            if (solid) part.gameObject.AddComponent<BoxCollider>().size = size;
            return part;
        }

        /// <summary>The downloaded model, as glTFast imported it.</summary>
        public static GameObject Model(string id) => Load<GameObject>($"{Folder}/PolyHaven/{id}/{id}_1k.gltf");

        /// <summary>A model from Sketchfab (CC Attribution or CC0; who made it is in progress/art-fetched.md and CREDITS.md).</summary>
        public static GameObject Sketchfab(string name) => Load<GameObject>($"{Folder}/Sketchfab/{name}/{name}.glb");

        /// <summary>
        /// A model placed in its parent at a given height, standing with the middle of its base on a point,
        /// turned as given. Sizes come from the meshes themselves through every transform inside the model
        /// (downloaded models carry their own turns and scales). It casts shadows; no collider is added.
        /// </summary>
        public static GameObject Place(GameObject model, Transform parent, Vector3 foot, float height, Quaternion turn)
        {
            var made = (GameObject)PrefabUtility.InstantiatePrefab(model);
            made.transform.SetParent(parent, false);
            made.transform.localPosition = Vector3.zero;
            made.transform.localRotation = turn;
            made.transform.localScale = Vector3.one;
            var box = Measure(made, parent);
            float scale = height / Mathf.Max(0.001f, box.size.y);
            made.transform.localScale = Vector3.one * scale;
            box = Measure(made, parent);
            made.transform.localPosition += foot - new Vector3(box.center.x, box.min.y, box.center.z);
            foreach (var r in made.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return made;
        }

        /// <summary>What the object draws, as a box in another transform's space (its meshes through every transform).</summary>
        public static Bounds Measure(GameObject made, Transform space)
        {
            var toSpace = space ? space.worldToLocalMatrix : Matrix4x4.identity;
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in made.GetComponentsInChildren<Renderer>())
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (!mesh) continue;
                var b = mesh.bounds;
                var m = toSpace * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one));
                    if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                    else bounds.Encapsulate(corner);
                }
            }
            return bounds;
        }

        /// <summary>
        /// A model placed in its parent, or one piece of a kit (by its name in the file), turned as given
        /// (the piece's own turn in the file is replaced). It casts shadows; when solid it gets a box
        /// collider round what it draws.
        /// </summary>
        public static GameObject Prop(string id, Transform parent, Vector3 at, Quaternion turn, string piece = null, bool solid = true, float scale = 1f)
        {
            var model = Model(id);
            GameObject made;
            if (piece == null) made = (GameObject)PrefabUtility.InstantiatePrefab(model);
            else
            {
                var source = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == piece)
                             ?? throw new System.InvalidOperationException($"{id} has no piece named {piece}");
                made = Object.Instantiate(source.gameObject);
                made.name = piece;
            }
            made.transform.SetParent(parent, false);
            made.transform.localPosition = at;
            made.transform.localRotation = turn;
            made.transform.localScale = Vector3.one * scale;
            foreach (var r in made.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            if (solid) Fit(made);
            return made;
        }

        /// <summary>A copy of a model's material, kept as an asset, changed as given (a lamp lit or dead).</summary>
        public static Material Variant(Material original, string name, System.Action<Material> change)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(original);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = original.shader;
                material.CopyPropertiesFromMaterial(original);
            }
            change(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A box collider round everything the object draws, in its own space.</summary>
        static void Fit(GameObject made)
        {
            var filters = made.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return;
            var toLocal = made.transform.worldToLocalMatrix;
            var bounds = new Bounds();
            bool first = true;
            foreach (var f in filters)
            {
                if (!f.sharedMesh) continue;
                var b = f.sharedMesh.bounds;
                var m = toLocal * f.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one));
                    if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                    else bounds.Encapsulate(corner);
                }
            }
            var box = made.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        static string Find(string folder, string ending, string middle)
        {
            if (!Directory.Exists(folder)) return null;
            return Directory.GetFiles(folder).Select(p => p.Replace('\\', '/'))
                .FirstOrDefault(p => p.EndsWith(ending) || p.Contains(middle) && p.EndsWith(".jpg"));
        }

        static void Normal(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The set's mask as URP wants it: metal in red, occlusion in green, smoothness in alpha. Made once
        /// from the set's own maps (Poly Haven packs them as ARM: occlusion, roughness, metal).
        /// </summary>
        static Texture2D Mask(string folder)
        {
            string path = $"{folder}/{Path.GetFileName(folder)}_Mask.png";
            if (!File.Exists(path))
            {
                Color32[] Read(string file, out int width)
                {
                    width = 0;
                    if (file == null) return null;
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    t.LoadImage(File.ReadAllBytes(file));
                    width = t.width;
                    var pixels = t.GetPixels32();
                    Object.DestroyImmediate(t);
                    return pixels;
                }
                var arm = Read(Find(folder, "_arm.jpg", "_arm_"), out int size);
                Color32[] rough = null, metal = null, occlusion = null;
                if (arm == null)
                {
                    rough = Read(Find(folder, "_Roughness.jpg", "_rough_"), out size);
                    metal = Read(Find(folder, "_Metalness.jpg", "_metal_"), out _);
                    occlusion = Read(Find(folder, "_AmbientOcclusion.jpg", "_ao_"), out _);
                }
                int count = (arm ?? rough).Length;
                var mask = new Color32[count];
                for (int i = 0; i < count; i++)
                {
                    byte m = arm != null ? arm[i].b : metal != null && metal.Length == count ? metal[i].r : (byte)0;
                    byte o = arm != null ? arm[i].r : occlusion != null && occlusion.Length == count ? occlusion[i].r : (byte)255;
                    byte r = arm != null ? arm[i].g : rough[i].r;
                    mask[i] = new Color32(m, o, 0, (byte)(255 - r));
                }
                var texture = new Texture2D(size, count / size, TextureFormat.RGBA32, true, true);
                texture.SetPixels32(mask);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return Load<Texture2D>(path);
        }
    }
}
