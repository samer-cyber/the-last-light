using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.Building.SceneKit;

namespace Game.Building
{
    /// <summary>
    /// What makes Sunvale a town that died rather than boxes in snow: a power line down the west side of the main
    /// street with its wires sagging pole to pole and snapped at the end, street lamps down the east side (a few still
    /// burning), rusted cars and Ima's school bus sunk in drifts, the school itself on the square with its swings and
    /// slide, dead trees black against the sun, the welcome sign at the edge of town; mist along the streets and snow
    /// blown along the ground. And the chasm: the old road bridge broken in the middle, its two ends reaching out over
    /// a gulf of mist with no bottom to see. Every model is CC BY from Sketchfab (CREDITS.md).
    /// </summary>
    public static partial class LastLightSceneBuilder
    {
        const string ModelRoot = "Assets/Game/Art/Sketchfab";
        static readonly Dictionary<Material, Material> litModels = new Dictionary<Material, Material>();
        /// <summary>Where MakeTown put its houses (their centres), so nothing else is set inside one.</summary>
        static readonly List<Vector3> houseSpots = new List<Vector3>();

        /// <summary>A URP Lit copy of a model's glTF material, with its own textures and a faint glow of its own colour.</summary>
        static Material LitModel(Material m, string model, float glow)
        {
            if (!m) return m;
            if (litModels.TryGetValue(m, out var known)) return known;
            string safe = new string(m.name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            string path = $"{ModelRoot}/{model}/{model} {safe} Lit.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!copy)
            {
                copy = new Material(lit);
                AssetDatabase.CreateAsset(copy, path);
            }
            copy.shader = lit;
            Texture Find(params string[] names)
            {
                foreach (var n in names)
                    if (m.HasProperty(n) && m.GetTexture(n)) return m.GetTexture(n);
                return null;
            }
            Color Tint(params string[] names)
            {
                foreach (var n in names)
                    if (m.HasProperty(n)) return m.GetColor(n);
                return Color.white;
            }
            var colour = Find("baseColorTexture", "diffuseTexture", "_BaseMap", "_MainTex");
            var normal = Find("normalTexture", "_BumpMap");
            var tint = Tint("baseColorFactor", "diffuseFactor", "_BaseColor", "_Color");
            copy.SetTexture("_BaseMap", colour);
            copy.SetColor("_BaseColor", tint);
            copy.SetFloat("_Smoothness", 0.14f);
            copy.SetFloat("_Metallic", 0f);
            copy.SetFloat("_Cull", 0f);
            if (normal)
            {
                copy.SetTexture("_BumpMap", normal);
                copy.EnableKeyword("_NORMALMAP");
            }
            else
            {
                copy.SetTexture("_BumpMap", null);
                copy.DisableKeyword("_NORMALMAP");
            }
            bool clear = m.renderQueue >= 3000;
            bool cut = !clear && (m.renderQueue >= 2450 || m.IsKeywordEnabled("_ALPHATEST_ON"));
            copy.SetFloat("_Surface", clear ? 1f : 0f);
            copy.SetFloat("_Blend", 0f);
            copy.SetFloat("_SrcBlend", clear ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            copy.SetFloat("_DstBlend", clear ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            copy.SetFloat("_ZWrite", clear ? 0f : 1f);
            copy.SetFloat("_AlphaClip", cut ? 1f : 0f);
            copy.SetFloat("_Cutoff", 0.5f);
            if (clear) copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (cut) copy.EnableKeyword("_ALPHATEST_ON"); else copy.DisableKeyword("_ALPHATEST_ON");
            copy.SetOverrideTag("RenderType", clear ? "Transparent" : cut ? "TransparentCutout" : "Opaque");
            copy.renderQueue = clear ? (int)RenderQueue.Transparent : cut ? (int)RenderQueue.AlphaTest : -1;
            if (glow > 0f)
            {
                copy.SetTexture("_EmissionMap", colour);
                copy.SetColor("_EmissionColor", new Color(0.05f, 0.056f, 0.07f) * glow);
                copy.EnableKeyword("_EMISSION");
            }
            else
            {
                copy.SetColor("_EmissionColor", Color.black);
                copy.DisableKeyword("_EMISSION");
            }
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(copy);
            litModels[m] = copy;
            return copy;
        }

        static Bounds MeasureAll(GameObject thing)
        {
            var all = thing.GetComponentsInChildren<Renderer>();
            var b = all.Length > 0 ? all[0].bounds : new Bounds(thing.transform.position, Vector3.zero);
            foreach (var r in all) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>The turn about the vertical that lays a model's longest side along z (from its vertices, not its loose bounds).</summary>
        static float LongAxisYaw(GameObject thing)
        {
            var points = new List<Vector2>();
            foreach (var f in thing.GetComponentsInChildren<MeshFilter>())
            {
                if (!f.sharedMesh) continue;
                var toWorld = f.transform.localToWorldMatrix;
                var vertices = f.sharedMesh.vertices;
                int step = Mathf.Max(1, vertices.Length / 600);
                for (int i = 0; i < vertices.Length; i += step)
                {
                    var w = toWorld.MultiplyPoint3x4(vertices[i]);
                    points.Add(new Vector2(w.x, w.z));
                }
            }
            float best = 0f, bestArea = float.MaxValue, bestX = 0f, bestZ = 0f;
            for (float a = 0f; a < 180f; a += 1.5f)
            {
                float c = Mathf.Cos(a * Mathf.Deg2Rad), s = Mathf.Sin(a * Mathf.Deg2Rad);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var p in points)
                {
                    // Unity's turn about y: (x, z) -> (x cos + z sin, -x sin + z cos).
                    float x = p.x * c + p.y * s, z = -p.x * s + p.y * c;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                }
                float area = (maxX - minX) * (maxZ - minZ);
                if (area < bestArea - 1e-4f)
                {
                    bestArea = area;
                    best = a;
                    bestX = maxX - minX;
                    bestZ = maxZ - minZ;
                }
            }
            return bestX > bestZ ? best + 90f : best;
        }

        /// <summary>
        /// A model set down in the world from a holder that turns, sizes and places it: its longest side (or its height) made
        /// the given size, its base set on the ground at <paramref name="at"/> (y is the ground) and sunk into the snow.
        /// </summary>
        static Transform ModelAt(string model, Transform parent, string name, Vector3 at, Quaternion turn, float size, bool byHeight, float sink,
            float glow, bool alongZ = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/{model}/{model}.glb");
            if (!prefab)
            {
                Debug.LogWarning($"Town: the model {model} is missing");
                return null;
            }
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var pivot = new GameObject(model).transform;
            pivot.SetParent(holder, false);
            Object.Instantiate(prefab, pivot).name = model + " Model";
            if (alongZ) pivot.localRotation = Quaternion.Euler(0f, LongAxisYaw(pivot.gameObject), 0f);
            var b = MeasureAll(holder.gameObject);
            float scale = byHeight ? size / Mathf.Max(b.size.y, 0.001f) : size / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.001f);
            holder.localScale = Vector3.one * scale;
            holder.rotation = turn;
            b = MeasureAll(holder.gameObject);
            holder.position += new Vector3(at.x - b.center.x, at.y - sink - b.min.y, at.z - b.center.z);
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sharedMaterials = r.sharedMaterials.Select(m => LitModel(m, model, glow)).ToArray();
            }
            return holder;
        }

        /// <summary>Where the top of a model is under a point: a ray cast down onto its own meshes (given colliders for it).</summary>
        static float RoofY(Transform thing, Vector3 at, float fallback)
        {
            foreach (var f in thing.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh && !f.GetComponent<Collider>()) f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
            Physics.SyncTransforms();
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(at.x, at.y + 30f, at.z), Vector3.down, 60f))
                if (hit.collider.transform.IsChildOf(thing) && hit.point.y > best) best = hit.point.y;
            return best > float.MinValue ? best : fallback;
        }

        /// <summary>How far the roof runs along a direction: rays down every half metre, those that hit within a little of its top.</summary>
        static (float top, float from, float to) RoofSpan(Transform thing, Vector3 centre, Vector3 along, float reach, float fallbackTop)
        {
            float top = RoofY(thing, centre, fallbackTop);
            float from = 0f, to = 0f;
            for (float d = -reach; d <= reach; d += 0.4f)
            {
                var p = centre + along * d;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, top + 20f, p.z), Vector3.down, 40f))
                    if (hit.collider.transform.IsChildOf(thing) && hit.point.y > top - 0.35f)
                    {
                        from = Mathf.Min(from, d);
                        to = Mathf.Max(to, d);
                        break;
                    }
            }
            return (top, from, to);
        }

        /// <summary>
        /// One pole out of the street lights pack, on its own: its foot at <paramref name="at"/>, turned so its wires run
        /// along <paramref name="run"/>, leaning a little. Gives back where its wires hang from, and where its lamp is.
        /// </summary>
        static Transform PoleAt(string part, Transform parent, Vector3 at, Vector3 run, float lean, float scale, out Vector3[] hangs, out Vector3 lamp)
        {
            hangs = new Vector3[0];
            lamp = at;
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/TownPoles/TownPoles.glb");
            if (!pack) return null;
            var copy = Object.Instantiate(pack);
            var mine = copy.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith(part)).ToArray();
            if (mine.Length == 0)
            {
                Object.DestroyImmediate(copy);
                return null;
            }
            // The pole's tip is its highest point; its foot the lowest.
            Vector3 tip = Vector3.down * 1e6f;
            float foot = float.MaxValue;
            float far = float.MinValue;
            foreach (var r in mine)
            {
                var f = r.GetComponent<MeshFilter>();
                if (!f || !f.sharedMesh) continue;
                var toWorld = r.transform.localToWorldMatrix;
                foreach (var v in f.sharedMesh.vertices)
                {
                    var w = toWorld.MultiplyPoint3x4(v);
                    if (w.y > tip.y) tip = w;
                    foot = Mathf.Min(foot, w.y);
                    far = Mathf.Max(far, w.z);
                }
            }
            var holder = new GameObject("Pole " + part).transform;
            holder.SetParent(parent, false);
            holder.position = new Vector3(tip.x, foot, tip.z);
            foreach (var r in mine)
            {
                r.transform.SetParent(holder, true);
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sharedMaterials = r.sharedMaterials.Select(m => LitModel(m, "TownPoles", 0.4f)).ToArray();
            }
            Object.DestroyImmediate(copy);
            float top = tip.y - foot;
            // In the pack the wires run along x and the crossarms along z.
            var local = new[] { new Vector3(0f, top - 0.42f, -0.85f), new Vector3(0f, top - 0.42f, -0.32f), new Vector3(0f, top - 0.42f, 0.32f), new Vector3(0f, top - 0.42f, 0.85f) };
            var lampLocal = new Vector3(0f, top - 0.3f, far - tip.z - 0.3f);
            // Wires (local x) along the run; the lamp arm (local z) turned to the street.
            var turn = Quaternion.LookRotation(Vector3.Cross(run.normalized, Vector3.up) * -1f, Vector3.up);
            var leanTurn = Quaternion.AngleAxis(lean, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right);
            holder.localScale = Vector3.one * scale;
            holder.SetPositionAndRotation(at, leanTurn * turn);
            hangs = local.Select(p => holder.TransformPoint(p)).ToArray();
            lamp = holder.TransformPoint(lampLocal);
            return holder;
        }

        /// <summary>A wire hanging between two points, sagging in the middle.</summary>
        static LineRenderer Cable(Transform parent, Material material, Vector3 a, Vector3 b, float sag, float width = 0.032f)
        {
            var line = new GameObject("Wire").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            const int n = 16;
            line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t));
            }
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A wire snapped at one end: it hangs from its pole and curls down onto the snow (or into the dark).</summary>
        static LineRenderer Snapped(Transform parent, Material material, Vector3 from, Vector3 toward, float drop, float reach)
        {
            var line = new GameObject("Snapped Wire").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            const int n = 14;
            line.positionCount = n;
            var dir = new Vector3(toward.x, 0f, toward.z).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                // Out a little from the pole, then straight down, as a cut wire falls.
                line.SetPosition(i, from + dir * reach * Mathf.Sin(t * Mathf.PI * 0.5f) + Vector3.down * drop * t * t);
            }
            line.widthMultiplier = 0.03f;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        /// <summary>Soft puffs of mist drifting in a box.</summary>
        static ParticleSystem MistAt(string name, Transform parent, Material material, Vector3 centre, Vector3 box, Vector2 size, Vector2 life,
            Vector3 slowest, Vector3 fastest, float alpha, int most)
        {
            var mist = MakeParticles(name, parent, material);
            mist.transform.position = centre;
            var main = mist.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = life.y;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.maxParticles = most;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = mist.emission;
            emission.rateOverTime = most / ((life.x + life.y) * 0.5f);
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var velocity = mist.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(slowest.x, fastest.x);
            velocity.y = new ParticleSystem.MinMaxCurve(slowest.y, fastest.y);
            velocity.z = new ParticleSystem.MinMaxCurve(slowest.z, fastest.z);
            var spin = mist.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            var grow = mist.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));
            var colour = mist.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 20f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mist;
        }

        /// <summary>A painted board on two posts, which can be looked at closely.</summary>
        static Transform Signboard(string name, Transform parent, Mesh cube, Mesh quad, Material post, Texture2D paint, string title,
            Vector3 at, Vector3 readFrom, float width, float height, float raise, float lean, string words = null)
        {
            var sign = new GameObject(name).transform;
            sign.SetParent(parent, false);
            sign.position = at;
            // The face (a quad, seen from its -z) toward whoever reads it.
            sign.rotation = Quaternion.LookRotation(-readFrom, Vector3.up) * Quaternion.Euler(lean, 0f, lean * 0.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                var leg = MakePart("Post", sign, cube, post, 1f);
                leg.transform.localPosition = new Vector3(side * width * 0.42f, (raise + height) * 0.5f - 0.4f, 0.06f);
                leg.transform.localScale = new Vector3(0.14f, raise + height + 0.8f, 0.14f);
                leg.shadowCastingMode = ShadowCastingMode.On;
            }
            var back = MakePart("Board", sign, cube, post, 1f);
            back.transform.localPosition = new Vector3(0f, raise + height * 0.5f, 0.02f);
            back.transform.localScale = new Vector3(width + 0.12f, height + 0.12f, 0.06f);
            back.shadowCastingMode = ShadowCastingMode.On;
            var painted = MakeMaterial(name + " Paint", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", paint);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.1f);
                m.SetTexture("_EmissionMap", paint);
                m.SetColor("_EmissionColor", new Color(0.32f, 0.32f, 0.34f));
                m.EnableKeyword("_EMISSION");
            });
            var face = MakePart("Face", sign, quad, painted, 1f);
            face.transform.localPosition = new Vector3(0f, raise + height * 0.5f, -0.015f);
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = new Vector3(width, height, 1f);
            face.receiveShadows = true;
            face.gameObject.AddComponent<Readable>().Set(paint, title, words);
            return sign;
        }

        /// <summary>
        /// The town's detail: power line, lamps, cars, the school and its bus, dead trees, drifts, the welcome sign, mist.
        /// </summary>
        static void MakeTownDetail(Transform world, Mesh cube, Mesh cylinder, Mesh ball, Mesh quad, Mesh billboard, Material snow, Material ground,
            Material metal, Texture2D softDot, List<Landmark> landmarks)
        {
            litModels.Clear();
            var root = new GameObject("Town Detail").transform;
            root.SetParent(world, false);
            var random = new System.Random(11);
            Random.InitState(11);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector3 On(float x, float z) => new Vector3(x, Height(x, z), z);
            // What already stands somewhere, and how much room it needs.
            var taken = houseSpots.Select(h => (at: new Vector2(h.x, h.z), room: 6.5f)).ToList();
            bool Clear(float x, float z, float room)
            {
                foreach (var t in taken)
                    if ((t.at - new Vector2(x, z)).magnitude < Mathf.Max(room, t.room)) return false;
                return true;
            }
            var wire = MakeMaterial("Power Wire", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(0.012f, 0.01f, 0.01f)));
            var lampGlow = MakeMaterial("Lamp Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.66f, 0.36f));
                m.SetFloat("_Edge", 0.14f);
                m.SetFloat("_Intensity", 0.7f);
            });
            var wood = MakeLit("Sign Wood", new Color(0.16f, 0.12f, 0.09f), 0.12f, 0f);
            var lump = SaveMesh("Snow Drift", mesh =>
            {
                var vertices = ball.vertices.Select(v => v * (1f + 0.16f * (Mathf.PerlinNoise(v.x * 2.3f + 4.1f, v.z * 2.3f + v.y * 1.7f + 2.2f) - 0.5f))).ToArray();
                mesh.vertices = vertices;
                mesh.uv = ball.uv;
                mesh.colors = Enumerable.Repeat(new Color(0f, 0f, 0f, 1f), vertices.Length).ToArray();
                mesh.triangles = ball.triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });

            // ---- The power line down the west side of the main street, wires pole to pole, snapped at the chasm ----
            var line = new GameObject("Power Line").transform;
            line.SetParent(root, false);
            string[] kinds = { "pillar_04", "pillar_02", "pillar_06", "pillar_01", "pillar_03", "pillar_02", "pillar_07", "pillar_06", "pillar_05" };
            Vector3[] last = null;
            int span = 0;
            for (float z = TownNear - 4f; z < ChasmNear - 2f; z += R(15.5f, 17.5f), span++)
            {
                float x = -7.6f + R(-0.4f, 0.4f);
                float lean = span == 4 ? 11f : R(0f, 5f);
                var pole = PoleAt(kinds[span % kinds.Length], line, On(x, z) + Vector3.down * 0.3f, Vector3.forward, lean, 1.3f, out var hangs, out _);
                if (!pole) break;
                if (last != null)
                    for (int k = 0; k < hangs.Length; k++)
                    {
                        // One span has lost two wires: they hang from the poles to the snow.
                        if (span == 3 && (k == 0 || k == 2))
                        {
                            Snapped(line, wire, last[k], hangs[k] - last[k], last[k].y - Height(last[k].x, last[k].z) + 0.2f, 2.2f);
                            Snapped(line, wire, hangs[k], last[k] - hangs[k], hangs[k].y - Height(hangs[k].x, hangs[k].z) + 0.2f, 1.6f);
                            continue;
                        }
                        Cable(line, wire, last[k], hangs[k], R(0.55f, 1.05f));
                    }
                last = hangs;
            }
            // The line went on over the chasm to the station once; its wires hang down into the dark now.
            if (last != null)
                foreach (var h in last)
                    Snapped(line, wire, h, Vector3.forward, R(16f, 26f), R(3.5f, 6f));

            // ---- Street lamps down the east side; a few still catch a little light ----
            var lamps = new GameObject("Street Lamps").transform;
            lamps.SetParent(root, false);
            int lampIndex = 0;
            for (float z = TownNear + 3f; z < ChasmNear - 4f; z += R(17f, 20f), lampIndex++)
            {
                float x = 7.2f + R(-0.3f, 0.3f);
                var pole = PoleAt("pillar_08", lamps, On(x, z) + Vector3.down * 0.25f, Vector3.forward, lampIndex == 2 ? 9f : R(0f, 4f), 1.25f, out _, out var head);
                if (!pole) break;
                // Turned so the arm reaches over the street.
                pole.rotation = Quaternion.AngleAxis(180f, Vector3.up) * pole.rotation;
                head = pole.position + Quaternion.AngleAxis(180f, Vector3.up) * (head - pole.position);
                if (lampIndex % 2 == 0 || lampIndex == 5)
                {
                    var light = new GameObject("Lamp Light").AddComponent<Light>();
                    light.transform.SetParent(pole, true);
                    light.transform.position = head + Vector3.down * 0.25f;
                    light.type = LightType.Spot;
                    light.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    light.spotAngle = 115f;
                    light.innerSpotAngle = 40f;
                    light.range = 16f;
                    light.intensity = 5.5f;
                    light.color = new Color(1f, 0.7f, 0.4f);
                    light.shadows = LightShadows.None;
                    var glow = MakePart("Lamp Glow", pole, billboard, lampGlow, 1f);
                    glow.transform.position = head + Vector3.down * 0.12f;
                    glow.transform.localScale = Vector3.one * 2.2f / Mathf.Max(pole.lossyScale.x, 0.01f);
                    var flicker = light.gameObject.AddComponent<Flicker>();
                    Wire(flicker, ("glow", glow.transform));
                }
            }

            // ---- Cars, rusted and buried, and the drifts the wind left against them ----
            var cars = new GameObject("Cars").transform;
            cars.SetParent(root, false);
            // A bank of snow: its half-width, how high it shows above the ground, its length. Flat enough that the ground's
            // shader lays snow all over it, sunk so only the gentle top shows.
            void Drift(Transform under, Vector3 at, Vector3 size, float yaw)
            {
                float halfWidth = size.x * 0.5f, shows = size.y, halfHeight = Mathf.Min(shows / 0.4f, halfWidth * 0.38f);
                size = new Vector3(size.x, halfHeight * 2f, size.z);
                at.y = Height(at.x, at.z) - (halfHeight - Mathf.Min(shows, halfHeight * 0.45f));
                var drift = MakePart("Drift", under, lump, ground, 1f);
                drift.transform.position = at;
                drift.transform.rotation = Quaternion.Euler(R(-3f, 3f), yaw, R(-3f, 3f));
                // The sphere it is made from is 2 m across.
                drift.transform.localScale = size * 0.5f;
                drift.shadowCastingMode = ShadowCastingMode.On;
                drift.receiveShadows = true;
            }
            void Car(string model, float length, Vector3 at, float yaw, float sink, bool roof = true)
            {
                var car = ModelAt(model, cars, model.Replace("Town", "") + " Car", at, Quaternion.Euler(R(-3f, 3f), yaw, R(-4f, 4f)), length, false, sink, 0.25f, true);
                if (!car) return;
                taken.Add((new Vector2(at.x, at.z), 4f));
                var b = MeasureAll(car.gameObject);
                var along = Quaternion.Euler(0f, yaw, 0f);
                // Snow on its roof and bonnet, and drifted deep along the side the wind comes from.
                if (roof)
                {
                    float top = RoofY(car, b.center, b.max.y);
                    var cap = MakePart("Roof Snow", car, lump, snow, 1f);
                    cap.transform.position = new Vector3(b.center.x, top - 0.04f, b.center.z);
                    cap.transform.rotation = along;
                    cap.transform.localScale = new Vector3(1.3f, 0.26f, length * 0.42f) * 0.5f / car.lossyScale.x;
                    cap.shadowCastingMode = ShadowCastingMode.On;
                }
                Drift(cars, at + along * new Vector3(-1.5f, 0f, R(-0.5f, 0.5f)), new Vector3(4.6f, 0.45f, length * 1.2f), yaw);
            }
            Car("TownSedan", 4.8f, On(-4.3f, 36f), R(-8f, 8f), 0.35f, false);
            Car("TownHatch", 4.1f, On(4.6f, 44f), 180f + R(-6f, 6f), 0.4f);
            Car("TownVan", 4.4f, On(-4.6f, 52f), R(-6f, 6f), 0.3f);
            Car("TownSedan", 4.8f, On(1.2f, 90f), 35f, 0.45f, false);
            Car("TownHatch", 4.1f, On(-4.4f, 99f), R(-5f, 5f), 0.5f);
            Car("TownVan", 4.4f, On(4.5f, 106f), 180f + R(-8f, 8f), 0.35f);
            Car("TownHatch", 4.1f, On(-1.6f, 123f), -28f, 0.4f);
            Car("TownSedan", 4.8f, On(19.5f, 31f), 90f + R(-8f, 8f), 0.5f, false);
            Car("TownSedan", 4.8f, On(-20f, 96f), 90f + R(-8f, 8f), 0.55f, false);
            Car("TownVan", 4.4f, On(36f, 58f), R(0f, 360f), 0.5f);

            // ---- Ima's school on the square: the building, its name, the swings and slide, the bus that never left ----
            var school = new GameObject("School").transform;
            school.SetParent(root, false);
            Vector3 schoolAt = On(-36f, 70f);
            taken.Add((new Vector2(schoolAt.x, schoolAt.z), 12f));
            var building = ModelAt("TownChalet", school, "School Building", schoolAt, Quaternion.Euler(0f, 90f, 0f), 17f, false, 0.25f, 1f);
            Signboard("School Sign", school, cube, quad, wood, Load<Texture2D>($"{Root}/Textures/Signs/School Name.png"), "the school's sign",
                On(-25f, 64f), Vector3.right, 3.4f, 0.85f, 1.15f, 3f, "SUNVALE PRIMARY SCHOOL");
            ModelAt("TownSwings", school, "Swings", On(-23f, 60.5f), Quaternion.Euler(0f, 90f + R(-5f, 5f), R(-2f, 2f)), 5f, false, 0.35f, 0.3f);
            ModelAt("TownSlide", school, "Slide", On(-25.5f, 78.5f), Quaternion.Euler(0f, 160f, 0f), 3.2f, false, 0.4f, 0.3f);
            taken.Add((new Vector2(-23f, 60.5f), 4f));
            taken.Add((new Vector2(-25.5f, 78.5f), 4f));
            taken.Add((new Vector2(-15.5f, 74f), 6f));
            var bus = ModelAt("TownBus", school, "School Bus", On(-15.5f, 74f), Quaternion.Euler(R(-2f, 2f), 8f, R(-3f, -1f)), 9f, false, 0.7f, 0.35f, true);
            if (bus)
            {
                var b = MeasureAll(bus.gameObject);
                var along = Quaternion.Euler(0f, 8f, 0f) * Vector3.forward;
                var (top, from, to) = RoofSpan(bus, b.center, along, 6f, b.max.y);
                var cap = MakePart("Roof Snow", bus, lump, snow, 1f);
                cap.transform.position = new Vector3(b.center.x, top - 0.06f, b.center.z) + along * ((from + to) * 0.5f);
                cap.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
                cap.transform.localScale = new Vector3(1.9f, 0.3f, Mathf.Max(2f, (to - from) * 0.92f)) * 0.5f / bus.lossyScale.x;
                Drift(school, On(-17.8f, 74f), new Vector3(5.5f, 0.75f, 11f), 8f);
            }
            landmarks.Add(MakeLandmark("School Place", school, schoolAt + Vector3.up * 3f, "SCHOOL", "Ima's school",
                "Sunvale Primary School on the square, where Ima went: its name board, rusted swings and a slide in the snow, the yellow school bus buried to its windows in front",
                new Vector3(16f, 8f, 16f)));

            // ---- Dead trees, black against the sun ----
            var trees = new GameObject("Dead Trees").transform;
            trees.SetParent(root, false);
            var treeSpots = new List<Vector2>();
            for (int i = 0; i < 90 && treeSpots.Count < 34; i++)
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                float band = (float)random.NextDouble();
                float x = side * (band < 0.4f ? R(18f, 21f) : band < 0.75f ? R(34f, 39f) : R(54f, 72f));
                float z = R(TownNear - 2f, TownFar + 12f);
                if (!Clear(x, z, 0f)) continue;
                if (treeSpots.Any(t => (t - new Vector2(x, z)).magnitude < 7f)) continue;
                treeSpots.Add(new Vector2(x, z));
            }
            // A few along the way from the vault, and at the chasm's edge.
            treeSpots.AddRange(new[] { new Vector2(-11f, 8f), new Vector2(12.5f, 14f), new Vector2(-14f, 19f), new Vector2(10f, 131f), new Vector2(-15f, 133f), new Vector2(24f, 128f) });
            int treeIndex = 0;
            foreach (var t in treeSpots)
            {
                bool tall = treeIndex++ % 3 != 0;
                ModelAt(tall ? "DeadTreeTall" : "DeadTree", trees, "Dead Tree", On(t.x, t.y), Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f)),
                    tall ? R(7f, 10.5f) : R(5f, 7.5f), true, 0.35f, 0f);
            }
            // One fallen across a yard.
            ModelAt("DeadTreeTall", trees, "Fallen Tree", On(20f, 84f), Quaternion.Euler(0f, 30f, 84f), 8.5f, true, -0.3f, 0f);

            // ---- The dead forest beyond the town: groves on the valley's sides and past the chasm ----
            var forest = new GameObject("Dead Forest").transform;
            forest.SetParent(root, false);
            var groves = new List<Vector2>();
            for (int i = 0; i < 400 && groves.Count < 18; i++)
            {
                float x = R(-170f, 170f), z = R(-20f, 250f);
                bool beyond = z > 162f;
                var p = new Vector2(x, z);
                if (!beyond && Mathf.Abs(x) < 86f) continue;
                if (z > 130f && z < 164f) continue;
                if (beyond && x > -34f && x < 64f) continue;
                if ((p - StationAt).magnitude < 32f || (p - Arena).magnitude < 46f || (p - Lake).magnitude < 50f || (p - VaultHill).magnitude < 42f) continue;
                if (groves.Any(g => (g - p).magnitude < 28f)) continue;
                groves.Add(p);
            }
            foreach (var g in groves)
            {
                int count = random.Next(4, 9);
                for (int k = 0; k < count; k++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = R(0f, 11f);
                    bool tall = random.NextDouble() < 0.6;
                    ModelAt(tall ? "DeadTreeTall" : "DeadTree", forest, "Dead Tree", On(g.x + Mathf.Cos(a) * r, g.y + Mathf.Sin(a) * r),
                        Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), tall ? R(8f, 12f) : R(5f, 8f), true, 0.4f, 0f);
                }
            }

            // ---- Drifts against the houses, on the side the wind comes from ----
            var drifts = new GameObject("Drifts").transform;
            drifts.SetParent(root, false);
            foreach (var h in houseSpots)
            {
                // Banked against the west wall (the wind's side), out of the street.
                float x = h.x < 0f ? h.x - 6.5f : h.x + 6.5f;
                Drift(drifts, On(x, h.z + R(-2f, 2f)), new Vector3(R(6f, 7.5f), R(0.45f, 0.7f), R(9f, 12f)), R(-12f, 12f));
            }

            // ---- The welcome sign at the edge of town, read on the way in from the vault ----
            Signboard("Welcome Sign", root, cube, quad, wood, Load<Texture2D>($"{Root}/Textures/Signs/Town Welcome.png"), "the sign at the edge of town",
                On(6.4f, 17f), Vector3.back, 3.2f, 1.6f, 1.1f, 4f, "WELCOME TO SUNVALE. POP. 2,180");
            landmarks.Add(MakeLandmark("Welcome Sign Place", root, On(6.4f, 17f) + Vector3.up * 1.9f, "TOWN_SIGN", "the town's sign",
                "a weathered sign at the edge of town: \"WELCOME TO SUNVALE. POP. 2,180\"", new Vector3(3.4f, 1.8f, 0.6f)));

            // ---- The way to the sun mirror: two road signs on the main street, and the town's old poster of it ----
            var mirrorRoad = Load<Texture2D>($"{Root}/Textures/Signs/Mirror Road.png");
            Signboard("Mirror Road Sign", root, cube, quad, wood, mirrorRoad, "a road sign", On(4.4f, 27f), Vector3.back, 1.5f, 1f, 1.3f, 3f,
                "SUN MIRROR. CLIFF ROAD, STRAIGHT ON (an arrow pointing north, through the town and over the bridge)");
            Signboard("Mirror Road Sign", root, cube, quad, wood, mirrorRoad, "a road sign", On(4.4f, 124f), Vector3.back, 1.5f, 1f, 1.3f, -4f,
                "SUN MIRROR. CLIFF ROAD, STRAIGHT ON (an arrow pointing north, over the bridge)");
            Signboard("Mirror Poster", root, cube, quad, wood, Load<Texture2D>($"{Root}/Textures/Signs/Mirror Poster.png"), "the town's old poster of the sun mirror",
                On(-9f, 63f), Vector3.right, 2.4f, 1.5f, 0.9f, -3f,
                "SUNVALE SUN MIRROR. IT KEEPS THE VALLEY WARM. (a painting of the great dish on its tower on the clifftop north of town, catching the sun and throwing its light down onto the town and the vault)");
            var posterLamp = new GameObject("Poster Lamp").AddComponent<Light>();
            posterLamp.transform.SetParent(root, false);
            posterLamp.transform.position = On(-9f, 63f) + new Vector3(1.6f, 3.4f, 0f);
            posterLamp.transform.rotation = Quaternion.LookRotation(On(-9f, 63f) + Vector3.up * 1.6f - posterLamp.transform.position, Vector3.up);
            posterLamp.type = LightType.Spot;
            posterLamp.spotAngle = 75f;
            posterLamp.innerSpotAngle = 40f;
            posterLamp.range = 8f;
            posterLamp.intensity = 5f;
            posterLamp.color = new Color(1f, 0.82f, 0.58f);
            posterLamp.shadows = LightShadows.None;
            posterLamp.gameObject.AddComponent<Flicker>();
            landmarks.Add(MakeLandmark("Mirror Poster Place", root, On(-9f, 63f) + Vector3.up * 1.7f, "MIRROR_POSTER", "the old poster of the sun mirror",
                "a faded town poster by the square: SUNVALE SUN MIRROR, IT KEEPS THE VALLEY WARM, with a painting of the dish on the clifftop north of town sending its light down to the town",
                new Vector3(2.6f, 1.7f, 0.6f)));

            // ---- Mist lying in the streets ----
            var puff = MakeTexture("Mist Puff", 128, true, (u, v) =>
            {
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float n = 0f, amp = 0.5f, f = 3f;
                for (int o = 0; o < 4; o++, amp *= 0.5f, f *= 2.1f) n += amp * Mathf.PerlinNoise(u * f + 13.1f, v * f + 7.3f);
                float edge = Mathf.Clamp01(1f - r);
                float a = Mathf.Clamp01(edge * edge * (0.35f + 1.3f * n) - 0.05f);
                return new Color(1f, 1f, 1f, a);
            });
            var streetMist = MakeMaterial("Street Mist", "Game/Mist", m =>
            {
                m.SetTexture("_MainTex", puff);
                m.SetColor("_Color", new Color(0.36f, 0.27f, 0.27f, 1f));
                m.SetFloat("_Softness", 1.5f);
                m.SetFloat("_NearFade", 5f);
            });
            MistAt("Street Mist", root, streetMist, new Vector3(0f, 1.1f, 72f), new Vector3(150f, 1.4f, 112f), new Vector2(9f, 16f), new Vector2(26f, 38f),
                new Vector3(0.3f, -0.02f, -0.1f), new Vector3(0.8f, 0.05f, 0.25f), 0.2f, 170);

            MakeChasm(world, cube, quad, puff, wire, snow, landmarks);
            Signboard("Bridge Sign", root, cube, quad, wood, Load<Texture2D>($"{Root}/Textures/Signs/Bridge Out.png"), "a warning sign at the road's end",
                On(-3.6f, ChasmNear - 7f), Vector3.back, 1.5f, 1f, 0.9f, -6f, "DANGER. BRIDGE OUT");
        }

        /// <summary>
        /// The chasm made deep: mist filling it from far below, thinning as it rises, spilling over the rim near the
        /// road; a sheet of it lower down so there is no bottom to see; and the old road bridge, broken in the middle.
        /// </summary>
        static void MakeChasm(Transform world, Mesh cube, Mesh quad, Texture2D puff, Material wire, Material snow, List<Landmark> landmarks)
        {
            var root = new GameObject("Chasm Depths").transform;
            root.SetParent(world, false);
            float mid = (ChasmNear + ChasmFar) * 0.5f;
            Material Mist(string name, Color colour, float softness, float near) => MakeMaterial(name, "Game/Mist", m =>
            {
                m.SetTexture("_MainTex", puff);
                m.SetColor("_Color", colour);
                m.SetFloat("_Softness", softness);
                m.SetFloat("_NearFade", near);
                m.SetVector("_Drift", Vector4.zero);
            });
            var deep = Mist("Chasm Mist Deep", new Color(0.34f, 0.18f, 0.15f, 1f), 5f, 6f);
            var high = Mist("Chasm Mist High", new Color(0.56f, 0.32f, 0.26f, 1f), 3f, 5f);
            var rim = Mist("Chasm Mist Rim", new Color(0.62f, 0.38f, 0.31f, 1f), 1.5f, 4f);
            // From the depths, thick; then thinner and paler, catching the sun's last light; and wisps over the edge.
            MistAt("Mist Deep", root, deep, new Vector3(0f, -27f, mid), new Vector3(640f, 10f, 13f), new Vector2(14f, 26f), new Vector2(40f, 60f),
                new Vector3(0.15f, -0.04f, -0.05f), new Vector3(0.6f, 0.06f, 0.05f), 0.75f, 240);
            MistAt("Mist High", root, high, new Vector3(0f, -9.5f, mid), new Vector3(420f, 9f, 13.5f), new Vector2(7f, 13f), new Vector2(26f, 40f),
                new Vector3(0.2f, 0.05f, -0.08f), new Vector3(0.7f, 0.22f, 0.08f), 0.5f, 280);
            MistAt("Mist Rim", root, rim, new Vector3(0f, -2.5f, mid), new Vector3(110f, 3f, 14f), new Vector2(4f, 8f), new Vector2(14f, 22f),
                new Vector3(0.2f, 0.12f, -0.15f), new Vector3(0.6f, 0.45f, 0.15f), 0.26f, 130);
            // A sheet far down, so whatever is at the bottom is never seen.
            var sheetMaterial = MakeMaterial("Chasm Floor Mist", "Game/Mist", m =>
            {
                m.SetTexture("_MainTex", MakeTexture("Mist Sheet", 256, true, (u, v) =>
                {
                    // Tiles both ways: noise on a torus.
                    float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                    float n = 0.6f * Mathf.PerlinNoise(Mathf.Cos(a) * 1.3f + 5f, Mathf.Sin(a) * 1.3f + Mathf.Cos(b) * 1.3f + 3f)
                              + 0.4f * Mathf.PerlinNoise(Mathf.Sin(b) * 2.6f + 9f, Mathf.Cos(a) * 2.6f + 1f);
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(0.62f + 0.6f * (n - 0.5f)));
                }));
                m.SetTextureScale("_MainTex", new Vector2(24f, 1f));
                m.SetColor("_Color", new Color(0.24f, 0.13f, 0.11f, 1f));
                m.SetFloat("_Softness", 6f);
                m.SetFloat("_NearFade", 2f);
                m.SetVector("_Drift", new Vector4(0.004f, 0.001f, -0.002f, 0.0015f));
            });
            foreach (float depth in new[] { -33f, -21f })
            {
                var sheet = MakePart("Mist Sheet", root, quad, sheetMaterial, 1f);
                sheet.transform.position = new Vector3(0f, depth, mid);
                sheet.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                sheet.transform.localScale = new Vector3(660f, ChasmFar - ChasmNear + 8f, 1f);
            }

            // ---- The old road bridge: its two ends reach out over the chasm, ten metres short of each other ----
            MakeOldBridge(root, cube, wire, landmarks);
        }

        static void MakeOldBridge(Transform parent, Mesh cube, Material wire, List<Landmark> landmarks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/BrokenBridge/BrokenBridge.glb");
            if (!prefab) return;
            float mid = (ChasmNear + ChasmFar) * 0.5f;
            const float deckTop = 7.64f, half = 24.4f, gap = 5f;
            var holder = new GameObject("Old Bridge").transform;
            holder.SetParent(parent, false);
            var copy = Object.Instantiate(prefab, holder);
            copy.name = "BrokenBridge Model";
            // Cut out the middle: every triangle whose centre lies within the gap (in the bridge's length), with a ragged edge.
            foreach (var f in copy.GetComponentsInChildren<MeshFilter>())
            {
                var source = f.sharedMesh;
                if (!source) continue;
                var toBridge = f.transform.localToWorldMatrix;
                var vertices = source.vertices;
                var placed = vertices.Select(v => toBridge.MultiplyPoint3x4(v)).ToArray();
                string meshName = "Old Bridge " + new string(f.name.Where(char.IsLetterOrDigit).ToArray());
                var cut = SaveMesh(meshName, mesh =>
                {
                    mesh.indexFormat = source.indexFormat;
                    mesh.vertices = vertices;
                    mesh.normals = source.normals;
                    if (source.tangents.Length == vertices.Length) mesh.tangents = source.tangents;
                    mesh.uv = source.uv;
                    if (source.uv2.Length == vertices.Length) mesh.uv2 = source.uv2;
                    if (source.colors.Length == vertices.Length) mesh.colors = source.colors;
                    mesh.subMeshCount = source.subMeshCount;
                    for (int s = 0; s < source.subMeshCount; s++)
                    {
                        var tris = source.GetTriangles(s);
                        var kept = new List<int>(tris.Length);
                        for (int t = 0; t < tris.Length; t += 3)
                        {
                            var c = (placed[tris[t]] + placed[tris[t + 1]] + placed[tris[t + 2]]) / 3f;
                            float ragged = 1.6f * (Mathf.PerlinNoise(c.y * 0.55f + 3.3f, c.z * 0.8f + 7.1f) - 0.5f);
                            if (Mathf.Abs(c.x) < gap + ragged) continue;
                            kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                        }
                        mesh.SetTriangles(kept, s);
                    }
                    mesh.RecalculateBounds();
                });
                f.sharedMesh = cut;
                var r = f.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sharedMaterials = r.sharedMaterials.Select(m => LitModel(m, "BrokenBridge", 0.5f)).ToArray();
            }
            // Decks to walk on, out to the broken ends.
            foreach (float side in new[] { -1f, 1f })
            {
                var deck = new GameObject("Deck").AddComponent<BoxCollider>();
                deck.transform.SetParent(holder, false);
                float from = gap + 0.4f, to = half;
                deck.center = new Vector3(side * (from + to) * 0.5f, deckTop - 0.25f, 0.1f);
                deck.size = new Vector3(to - from, 0.5f, 5f);
            }
            // Along the world's z, each end level with its own ground.
            float yNear = Height(0f, mid - half + 3f), yFar = Height(0f, mid + half - 3f);
            float pitch = Mathf.Atan2(yFar - yNear, 2f * half - 6f) * Mathf.Rad2Deg;
            holder.rotation = Quaternion.AngleAxis(-pitch, Vector3.right) * Quaternion.Euler(0f, -90f, 0f);
            holder.position = new Vector3(0f, (yNear + yFar) * 0.5f - deckTop + 0.04f, mid);
            landmarks.Add(MakeLandmark("Old Bridge Place", holder, holder.TransformPoint(new Vector3(-gap - 2f, deckTop, 0f)), "OLD_BRIDGE", "the old bridge",
                "the town's old road bridge over the chasm, broken in the middle: its two ends reach out over the drop, about ten metres apart; nothing can walk across the gap",
                new Vector3(6f, 3f, 5f)));
        }

        /// <summary>Snow blown along the ground round the survivor: fine streaks on the wind, and gusts.</summary>
        static void BlowingSnow(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new Color(0.92f, 0.94f, 1f, 0.5f);
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = particles.emission;
            emission.rateOverTime = 200f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.5f, new ParticleSystem.MinMaxCurve(90f, 160f), -1, 4.5f) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 0.7f, 34f);
            shape.position = new Vector3(-6f, 0.25f, 4f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.6f;
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1.2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
