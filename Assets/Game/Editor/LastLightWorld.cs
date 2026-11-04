using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.Building.SceneKit;

namespace Game.Building
{
    /// <summary>
    /// The open world of the Last Light, along the north (+z): the vault dug into its hill at the
    /// south, the frozen town where Spark's girl lived, the chasm across the whole valley, the old
    /// station and the machines' ground beyond it, then a sheer cliff with the sun mirror on top.
    /// Open snowfields either way, a frozen lake to the west, dead machines to the east, and far off
    /// all round, past the blizzard, mountains. The caged sun sits low ahead the whole way.
    /// </summary>
    public static partial class LastLightSceneBuilder
    {
        const float TownNear = 25f, TownFar = 115f, ChasmNear = 140f, ChasmFar = 155f, CliffZ = 262f;
        /// <summary>Where the survivor stands outside the vault door.</summary>
        static readonly Vector3 Start = new Vector3(0f, 0f, -27f);
        static readonly Vector2 VaultHill = new Vector2(0f, -52f);
        const float VaultDoorZ = -33.5f;
        static readonly Vector2 StationAt = new Vector2(34f, 178f);
        static readonly Vector2 Arena = new Vector2(26f, 206f);
        static readonly Vector2 Lake = new Vector2(-105f, 70f);
        static readonly Vector2 MirrorAt = new Vector2(-14f, 278f);
        /// <summary>The middle of the world, and how far from it the blizzard begins.</summary>
        public static readonly Vector2 Middle = new Vector2(0f, 120f);
        public const float BlizzardFrom = 300f;

        /// <summary>The height of the ground anywhere.</summary>
        public static float Height(float x, float z)
        {
            float broad = Mathf.PerlinNoise(x * 0.008f + 3.1f, z * 0.008f + 7.7f) - 0.5f;
            float n = Mathf.PerlinNoise(x * 0.035f + 1.7f, z * 0.035f + 4.2f) - 0.5f;
            float fine = Mathf.PerlinNoise(x * 0.17f + 1.3f, z * 0.17f + 9.4f) - 0.5f;
            float sides = Smooth(45f, 160f, Mathf.Abs(x));
            float h = broad * (2.5f + 9f * sides) + n * 2.4f + fine * 0.3f;
            // The town lies nearly flat.
            float town = Inside(x, z, -75f, 75f, TownNear, TownFar, 18f);
            h = Mathf.Lerp(h, n * 0.5f + fine * 0.15f, town);
            // The vault's hill, and the cut in its north face where the door is.
            float hill = 1f - Smooth(0.45f, 1f, new Vector2((x - VaultHill.x) / 30f, (z - VaultHill.y) / 24f).magnitude);
            h += 12f * hill;
            float cut = Inside(x, z, -6.5f, 6.5f, VaultDoorZ, VaultDoorZ + 14f, 3f);
            h = Mathf.Lerp(h, n * 0.3f, cut);
            // A trodden way from the vault to the town.
            h = Mathf.Lerp(h, n * 0.5f, Inside(x, z, -9f, 9f, VaultDoorZ + 10f, TownNear, 10f) * 0.8f);
            // A frozen lake to the west, in a shallow hollow.
            h -= 1.4f * LakeAmount(x, z);
            // The far side is a little higher; the machines' ground before the station is trodden flat.
            float near = ChasmNear + 2.5f * (Mathf.PerlinNoise(x * 0.03f, 0.5f) - 0.5f);
            float far = ChasmFar + 2.5f * (Mathf.PerlinNoise(x * 0.03f + 5f, 2.5f) - 0.5f);
            if (z > far) h += 1.2f * Smooth(far, far + 4f, z);
            float arena = 1f - Smooth(34f, 58f, new Vector2(x - Arena.x, z - Arena.y).magnitude);
            h = Mathf.Lerp(h, 1.2f + n * 0.5f, arena);
            // The cliff: sheer, 28 m, all the way across; the shelf on top runs on to the mountains.
            float cliffAt = CliffZ + 7f * (Mathf.PerlinNoise(x * 0.02f, 4f) - 0.5f);
            h += 28f * Smooth(cliffAt - 1.2f, cliffAt + 1.2f, z) + 3f * Smooth(cliffAt, cliffAt + 60f, z) * (n + 0.5f);
            // The chasm: sheer walls, deep and dark.
            float chasm = Smooth(near - 0.5f, near + 0.4f, z) * (1f - Smooth(far - 0.4f, far + 0.5f, z));
            h = Mathf.Lerp(h, -45f + fine * 4f, chasm);
            // Mountains far off, past the blizzard.
            float r = new Vector2(x - Middle.x, (z - Middle.y) * 0.9f).magnitude;
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.0045f + 11f, z * 0.0045f + 2f) * 2f - 1f);
            h += Smooth(BlizzardFrom + 90f, BlizzardFrom + 420f, r) * (70f + 170f * ridge * ridge);
            return h;
        }

        /// <summary>1 inside a rectangle, falling to 0 over a margin outside it.</summary>
        static float Inside(float x, float z, float minX, float maxX, float minZ, float maxZ, float margin)
        {
            float dx = Mathf.Max(minX - x, 0f, x - maxX);
            float dz = Mathf.Max(minZ - z, 0f, z - maxZ);
            return 1f - Smooth(0f, margin, Mathf.Sqrt(dx * dx + dz * dz));
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float LakeAmount(float x, float z) => 1f - Smooth(0.7f, 1f, new Vector2((x - Lake.x) / 34f, (z - Lake.y) / 22f).magnitude);

        /// <summary>
        /// The ground as one mesh: fine (1.25 m) where anyone walks, coarse (10 m) out to the mountains.
        /// </summary>
        static Mesh MakeGround()
        {
            var xs = Steps(-820f, -170f, 10f, 170f, 1.25f, 820f);
            var zs = Steps(-700f, -80f, 10f, 330f, 1.25f, 960f);
            int nx = xs.Count, nz = zs.Count;
            var vertices = new Vector3[nx * nz];
            var colours = new Color[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = xs[i], z = zs[j];
                    vertices[j * nx + i] = new Vector3(x, Height(x, z), z);
                    colours[j * nx + i] = new Color(LakeAmount(x, z) > 0.3f ? 1f : 0f, 0f, 0f, 1f);
                }
            var triangles = new List<int>((nx - 1) * (nz - 1) * 6);
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    // No ground in the vault's passage: the hill's slope would cut through it (the hill is hidden in the
                    // portal's concrete everywhere else, and the passage has its own floor).
                    float cx = (xs[i] + xs[i + 1]) * 0.5f, cz = (zs[j] + zs[j + 1]) * 0.5f;
                    if (Mathf.Abs(cx) < 1.4f && cz < VaultDoorZ - 0.2f && cz > VaultDoorZ - 5.3f) continue;
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            return SaveMesh("Frozen Ground", mesh =>
            {
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.colors = colours;
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        /// <summary>Coarse from the first edge to the fine band, fine across it, coarse again to the last edge.</summary>
        static List<float> Steps(float from, float fineFrom, float coarse, float fineTo, float fine, float to)
        {
            var list = new List<float>();
            for (float v = from; v < fineFrom; v += coarse) list.Add(v);
            for (float v = fineFrom; v < fineTo; v += fine) list.Add(v);
            for (float v = fineTo; v <= to; v += coarse) list.Add(v);
            return list;
        }

        /// <summary>
        /// The frozen town: two rows of houses each side of a main street, cross streets, a square with a
        /// frozen fountain and the company's billboard, dead lamps, buried cars, bare trees. A third of the
        /// houses have fallen in. Snow on every roof.
        /// </summary>
        static void MakeTown(Transform parent, Mesh cube, Mesh cylinder, Mesh quad, Material wall, Material roof, Material snow, Material glass,
            Material metal, Material poster, List<Landmark> landmarks)
        {
            var root = new GameObject("Town").transform;
            root.SetParent(parent, false);
            houseSpots.Clear();
            var random = new System.Random(7);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Renderer Box(string name, Transform under, Material m, Vector3 at, Vector3 size, Quaternion turn)
            {
                var part = MakePart(name, under, cube, m, 1f);
                part.transform.position = at;
                part.transform.rotation = turn;
                part.transform.localScale = size;
                part.shadowCastingMode = ShadowCastingMode.On;
                part.receiveShadows = true;
                return part;
            }
            // Real houses under snow (Sketchfab, CC BY, credited in CREDITS.md), each with the side its door faces in its
            // own space. Standing houses are these; fallen ones stay as broken walls under snow.
            var models = new (GameObject prefab, Vector3 front)[]
            {
                (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Sketchfab/TownHouse/TownHouse.glb"), Vector3.left),
                (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Sketchfab/TownChalet/TownChalet.glb"), Vector3.forward),
                (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Sketchfab/TownCabin/TownCabin.glb"), Vector3.back),
            };
            if (models.Any(m => !m.prefab)) Debug.LogWarning("Town: a house model is missing; boxes stand in for it");
            // Their materials glow faintly in their own colours: the night has almost no light, and the rest of the
            // world's shaders carry their own fill, so without it the houses would be black shapes.
            var glows = new Dictionary<Material, Material>();
            Material Glow(Material m, string model)
            {
                if (!m) return m;
                if (glows.TryGetValue(m, out var known)) return known;
                // URP Lit with the model's own textures (glTFast's shader graph ignores emission here).
                string path = $"Assets/Game/Art/Sketchfab/{model}/{model} Glow.mat";
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!copy)
                {
                    copy = new Material(lit);
                    AssetDatabase.CreateAsset(copy, path);
                }
                copy.shader = lit;
                var colour = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
                var normal = m.HasProperty("normalTexture") ? m.GetTexture("normalTexture") : null;
                copy.SetTexture("_BaseMap", colour);
                copy.SetColor("_BaseColor", m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.white);
                copy.SetFloat("_Smoothness", 0.12f);
                copy.SetFloat("_Metallic", 0f);
                if (normal)
                {
                    copy.SetTexture("_BumpMap", normal);
                    copy.EnableKeyword("_NORMALMAP");
                }
                else copy.DisableKeyword("_NORMALMAP");
                copy.SetTexture("_EmissionMap", colour);
                copy.SetColor("_EmissionColor", new Color(0.05f, 0.056f, 0.07f));
                copy.EnableKeyword("_EMISSION");
                copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(copy);
                glows[m] = copy;
                return copy;
            }
            Bounds Measure(GameObject thing)
            {
                var all = thing.GetComponentsInChildren<Renderer>();
                var b = all.Length > 0 ? all[0].bounds : new Bounds(thing.transform.position, Vector3.zero);
                foreach (var r in all) b.Encapsulate(r.bounds);
                return b;
            }
            bool RealHouse(Transform house, Vector3 at, Vector3 facing, float ground, int index, bool fallen)
            {
                var (prefab, front) = models[(index * 7 + 3) % models.Length];
                if (!prefab) return false;
                // The model keeps its own root (Sketchfab's turn from Z-up, its units); a holder turns, sizes and places it.
                var model = new GameObject(prefab.name);
                model.transform.SetParent(house, false);
                Object.Instantiate(prefab, model.transform).name = prefab.name + " Model";
                model.transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Inverse(Quaternion.LookRotation(front)) * Quaternion.Euler(0f, R(-4f, 4f), 0f);
                var size = Measure(model).size;
                float scale = Mathf.Clamp(R(8.5f, 11f) / Mathf.Max(size.x, size.z), 0.6f, 1.6f);
                model.transform.localScale = Vector3.one * scale;
                if (fallen)
                {
                    // Caved in under two centuries of snow: tilted, and sunk to the windows.
                    model.transform.rotation = Quaternion.AngleAxis(R(6f, 13f), Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.right) * model.transform.rotation;
                }
                var b = Measure(model);
                float sink = fallen ? b.size.y * R(0.35f, 0.55f) : 0.2f;
                model.transform.position += new Vector3(at.x - b.center.x, ground - sink - b.min.y, at.z - b.center.z);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.sharedMaterials = r.sharedMaterials.Select(m => Glow(m, prefab.name)).ToArray();
                }
                return true;
            }
            void House(Vector3 at, float yaw, int index)
            {
                var house = new GameObject($"House {index}").transform;
                house.SetParent(root, false);
                houseSpots.Add(at);
                float w = R(6f, 9f), d = R(7f, 10f), h = R(4f, 7.5f);
                bool fallen = random.NextDouble() < 0.33;
                var turn = Quaternion.Euler(0f, yaw, 0f);
                float ground = Height(at.x, at.z);
                // Standing houses near the main street face it; the scattered ones face where they were built to.
                var facing = Mathf.Abs(at.x) < 32f ? new Vector3(-Mathf.Sign(at.x), 0f, 0f) : turn * Vector3.forward;
                if (RealHouse(house, at, facing, ground, index, fallen)) return;
                float tall = fallen ? h * R(0.35f, 0.7f) : h;
                Box("Walls", house, wall, new Vector3(at.x, ground + tall * 0.5f - 0.3f, at.z), new Vector3(w, tall + 0.6f, d), turn * Quaternion.Euler(fallen ? R(-3f, 3f) : 0f, 0f, fallen ? R(-4f, 4f) : 0f));
                if (!fallen)
                {
                    // A gable roof: two slabs leaning together, each under snow.
                    float pitch = 34f;
                    float half = w * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.3f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var slope = turn * Quaternion.Euler(0f, 0f, -side * pitch);
                        Vector3 centre = at + turn * new Vector3(side * w * 0.25f, 0f, 0f) + Vector3.up * (ground + h + w * 0.25f * Mathf.Tan(pitch * Mathf.Deg2Rad));
                        Box("Roof", house, roof, centre, new Vector3(half, 0.25f, d + 0.6f), slope);
                        Box("Roof Snow", house, snow, centre + slope * Vector3.up * 0.2f, new Vector3(half * 0.98f, 0.18f, d + 0.4f), slope);
                    }
                    // Dark windows along the street side.
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 window = at + turn * new Vector3((k - 1) * w * 0.3f, 0f, -d * 0.5f - 0.02f) + Vector3.up * (ground + h * 0.55f);
                        Box("Window", house, glass, window, new Vector3(0.9f, 1.1f, 0.06f), turn);
                    }
                }
                else Box("Rubble Snow", house, snow, new Vector3(at.x, ground + tall + 0.1f, at.z), new Vector3(w * 0.9f, 0.4f, d * 0.85f), turn * Quaternion.Euler(R(-6f, 6f), 0f, R(-6f, 6f)));
            }
            int count = 0;
            for (float z = TownNear + 6f; z < TownFar - 4f; z += R(10.5f, 13f))
            {
                if (Mathf.Abs(z - 70f) < 12f) continue; // the square
                foreach (float side in new[] { -1f, 1f })
                {
                    House(new Vector3(side * R(11f, 13f), 0f, z), side > 0 ? 270f : 90f, count++);
                    if (random.NextDouble() < 0.8) House(new Vector3(side * R(25f, 29f), 0f, z + R(-2f, 2f)), side > 0 ? 270f : 90f, count++);
                    if (random.NextDouble() < 0.45) House(new Vector3(side * R(42f, 50f), 0f, z + R(-3f, 3f)), R(0f, 360f), count++);
                }
            }
            // Lamps, the power line, cars, the school, trees and drifts: MakeTownDetail.
            // The square: a frozen fountain, and Lumen's billboard.
            float sy = Height(0f, 70f);
            var ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var basin = MakePart("Fountain Basin", root, cylinder, wall, 1f);
            basin.transform.position = new Vector3(0f, sy + 0.4f, 70f);
            basin.transform.localScale = new Vector3(6f, 0.4f, 6f);
            basin.shadowCastingMode = ShadowCastingMode.On;
            var pool = MakePart("Fountain Pool Ice", root, cylinder, glass, 1f);
            pool.transform.position = new Vector3(0f, sy + 0.62f, 70f);
            pool.transform.localScale = new Vector3(5.5f, 0.2f, 5.5f);
            var pillar = MakePart("Fountain Pillar", root, cylinder, wall, 1f);
            pillar.transform.position = new Vector3(0f, sy + 1.5f, 70f);
            pillar.transform.localScale = new Vector3(0.55f, 1.1f, 0.55f);
            pillar.shadowCastingMode = ShadowCastingMode.On;
            var bowl = MakePart("Fountain Bowl", root, cylinder, wall, 1f);
            bowl.transform.position = new Vector3(0f, sy + 2.62f, 70f);
            bowl.transform.localScale = new Vector3(2.3f, 0.16f, 2.3f);
            bowl.shadowCastingMode = ShadowCastingMode.On;
            // The water as it was the moment it froze: a jet up from the bowl, falling over in ropes, icicles round the rim.
            var column = MakePart("Fountain Ice", root, ball, glass, 1f);
            // (The sphere is 2 m across.)
            column.transform.position = new Vector3(0f, sy + 3.35f, 70f);
            column.transform.localScale = new Vector3(0.09f, 0.8f, 0.09f);
            // Where the jet broke and fell: a cap of ice over the bowl, ropes of it hanging to the pool.
            var crown = MakePart("Fountain Ice", root, ball, glass, 1f);
            crown.transform.position = new Vector3(0f, sy + 2.78f, 70f);
            crown.transform.localScale = new Vector3(1.2f, 0.22f, 1.2f);
            for (int k = 0; k < 16; k++)
            {
                var round = Quaternion.Euler(0f, k * 360f / 16f + 7f, 0f);
                var icicle = MakePart("Fountain Icicle", root, ball, glass, 1f);
                float drop = 0.5f + 1.5f * Mathf.PerlinNoise(k * 0.9f, 2.3f) * Mathf.PerlinNoise(k * 0.4f + 5f, 0.7f);
                icicle.transform.position = new Vector3(0f, sy + 2.6f - drop * 0.5f, 70f) + round * new Vector3(0f, 0f, 1.02f);
                float thick = 0.035f + 0.025f * (drop / 2f);
                icicle.transform.localScale = new Vector3(thick, drop * 0.5f, thick);
            }
            landmarks.Add(MakeLandmark("Fountain Place", root, new Vector3(0f, sy + 1.5f, 70f), "FOUNTAIN", "the frozen fountain",
                "the fountain in the town square, frozen mid-spray two hundred years ago", new Vector3(6f, 4f, 6f)));
            var board = new GameObject("Billboard").transform;
            board.SetParent(root, false);
            board.position = new Vector3(16f, sy, 78f);
            board.rotation = Quaternion.Euler(0f, 235f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                var leg = MakePart("Billboard Leg", board, cylinder, metal, 1f);
                leg.transform.localPosition = new Vector3(side * 3.2f, 2.5f, 0.2f);
                leg.transform.localScale = new Vector3(0.25f, 2.5f, 0.25f);
            }
            var frame = MakePart("Billboard Frame", board, cube, metal, 1f);
            frame.transform.localPosition = new Vector3(0f, 6.6f, 0.1f);
            frame.transform.localScale = new Vector3(8.6f, 4.6f, 0.2f);
            var face = MakePart("Poster Face", board, quad, poster, 1f);
            face.transform.localPosition = new Vector3(0f, 6.6f, -0.02f);
            face.transform.localScale = new Vector3(8.2f, 4.2f, 1f);
            face.shadowCastingMode = ShadowCastingMode.Off;
            var spot = new GameObject("Billboard Light").AddComponent<Light>();
            spot.transform.SetParent(board, false);
            spot.transform.localPosition = new Vector3(0f, 10.5f, -3.5f);
            spot.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.range = 16f;
            spot.intensity = 6f;
            spot.color = new Color(1f, 0.85f, 0.6f);
            spot.gameObject.AddComponent<Flicker>();
            landmarks.Add(MakeLandmark("Billboard Place", board, board.position + Vector3.up * 6.6f, "BILLBOARD", "the old billboard",
                "a faded Lumen advertisement over the square: \"LUMEN. We think, so you don't have to.\"", new Vector3(8.6f, 4.6f, 0.6f)));
            landmarks.Add(MakeLandmark("Town Place", root, new Vector3(0f, sy, 70f), "TOWN", "the frozen town",
                "Ima's town, where you lived before the cold: two hundred houses, twelve thousand people once, all gone into the vault or the snow; houses fallen in, cars buried, lamps dead",
                Vector3.zero));
        }

        /// <summary>The Lattice: a black net of mirrors round the sun, seen from the ground as a web across its face.</summary>
        static void MakeLattice(Mesh quad, Vector3 sunDirection)
        {
            var web = MakeTexture("Lattice", 512, true, (u, v) =>
            {
                // A hex web: distance to the nearest cell edge; some cells closed with black panels.
                Vector2 p = new Vector2(u, v) * 22f;
                Vector2 r = new Vector2(1f, 1.7320508f);
                Vector2 h = r * 0.5f;
                Vector2 a = new Vector2(Mod(p.x, r.x) - h.x, Mod(p.y, r.y) - h.y);
                Vector2 b = new Vector2(Mod(p.x - h.x, r.x) - h.x, Mod(p.y - h.y, r.y) - h.y);
                Vector2 g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
                Vector2 cell = p - g;
                Vector2 q = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
                float edge = 0.5f - Mathf.Max(Vector2.Dot(q, new Vector2(0.5f, 0.8660254f)), q.x);
                float line = 1f - Mathf.Clamp01((edge - 0.035f) / 0.03f);
                float hash = Mathf.Abs(Mathf.Sin(cell.x * 12.9898f + cell.y * 78.233f) * 43758.5453f) % 1f;
                float panel = hash < 0.22f ? 0.92f : 0f;
                float fade = Mathf.Clamp01((0.5f - new Vector2(u - 0.5f, v - 0.5f).magnitude) * 6f);
                return new Color(0f, 0f, 0f, Mathf.Max(line, panel) * fade);
            });
            var material = MakeMaterial("Lattice", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", web);
                m.SetColor("_BaseColor", new Color(0.01f, 0.008f, 0.008f, 1f));
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            });
            var lattice = MakePart("Lattice", null, quad, material, 1f);
            lattice.transform.position = sunDirection * 3000f;
            lattice.transform.rotation = Quaternion.LookRotation(sunDirection, Vector3.up);
            lattice.transform.localScale = Vector3.one * 4200f;
            lattice.shadowCastingMode = ShadowCastingMode.Off;
        }

        static float Mod(float a, float b) => a - b * Mathf.Floor(a / b);

        /// <summary>
        /// The machines' ground: the great dead hulks of machines a Harvester stripped, half sunk in the
        /// snow round the station, big enough to hide behind.
        /// </summary>
        static void MakeHulks(Transform parent, Mesh sphere, Mesh cylinder, Mesh cube, Material shell, Material eye, Material snow, List<Landmark> landmarks)
        {
            var root = new GameObject("Dead Machines").transform;
            root.SetParent(parent, false);
            var random = new System.Random(19);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + R(-12f, 12f);
                float distance = R(22f, 42f);
                Vector3 at = new Vector3(Arena.x, 0f, Arena.y) + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                if (Vector2.Distance(new Vector2(at.x, at.z), StationAt) < 12f) continue;
                at.y = Height(at.x, at.z);
                var hulk = new GameObject($"Hulk {i}").transform;
                hulk.SetParent(root, false);
                hulk.position = at;
                hulk.rotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-25f, 25f));
                float size = R(3.5f, 7f);
                Renderer Piece(string name, Mesh mesh, Material m, Vector3 at, Vector3 scale, Vector3 turn)
                {
                    var piece = MakePart(name, hulk, mesh, m, 1f);
                    piece.transform.localPosition = at;
                    piece.transform.localRotation = Quaternion.Euler(turn);
                    piece.transform.localScale = scale;
                    piece.shadowCastingMode = ShadowCastingMode.On;
                    return piece;
                }
                // The hull: a long angular box, broken open at one end, a plate torn loose.
                Piece("Hull", cube, shell, new Vector3(0f, size * 0.22f, 0f), new Vector3(size * 0.8f, size * 0.55f, size * 1.6f), Vector3.zero);
                Piece("Hull Cap", cube, shell, new Vector3(0f, size * 0.5f, size * 0.55f), new Vector3(size * 0.6f, size * 0.3f, size * 0.5f), new Vector3(R(-15f, 15f), 0f, R(-10f, 10f)));
                Piece("Plate", cube, shell, new Vector3(size * 0.5f, size * 0.25f, -size * 0.4f), new Vector3(0.15f, size * 0.6f, size * 0.7f), new Vector3(0f, R(-20f, 20f), R(15f, 40f)));
                Piece("Snow", cube, snow, new Vector3(0f, size * 0.5f + 0.05f, -size * 0.1f), new Vector3(size * 0.78f, 0.2f, size * 1.3f), Vector3.zero);
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Piece("Leg", cylinder, shell, new Vector3(side * size * 0.45f, size * 0.15f, R(-size, size) * 0.5f), new Vector3(0.45f, size * 0.55f, 0.45f), new Vector3(R(55f, 85f), R(0f, 360f), 0f));
                }
                if (random.NextDouble() < 0.45)
                    Piece("Dead Eye", sphere, eye, new Vector3(0f, size * 0.35f, size * 0.82f), Vector3.one * size * 0.14f, Vector3.zero);
            }
            landmarks.Add(MakeLandmark("Hulks Place", root, new Vector3(Arena.x, Height(Arena.x, Arena.y), Arena.y), "DEAD_MACHINES", "the dead machines",
                "the hulks of great machines half sunk in the snow round the station, stripped of their power by a Harvester; cover to hide behind", Vector3.zero));
        }
    }
}
