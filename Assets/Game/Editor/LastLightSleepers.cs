using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static Game.Building.SceneKit;

namespace Game.Building
{
    public static partial class LastLightSceneBuilder
    {
        // The shelter's people: one rigged man (Sketchfab, "Man" by Pumpkin, CC BY, a Character Creator base) in the
        // shelter's suit. The survivor wears him (Dress); every sleeper is him too, baked still in a resting pose.

        /// <summary>A bone's plain name: "mixamorig:LeftForeArm_010" is LeftForeArm, "CC_Base_L_Forearm_63" is L_Forearm.</summary>
        static string Canon(string raw) => Regex.Replace(Regex.Replace(Regex.Replace(raw, @"^.*:", ""), @"_\d+$", ""), @"^CC_Base_", "");

        /// <summary>Character Creator's names for the bones the rig follows, by their Mixamo names.</summary>
        static readonly Dictionary<string, string> CcBones = new Dictionary<string, string>
        {
            ["Hips"] = "Hip", ["Spine"] = "Waist", ["Spine1"] = "Spine01", ["Spine2"] = "Spine02", ["Neck"] = "NeckTwist01", ["Head"] = "Head",
            ["LeftShoulder"] = "L_Clavicle", ["LeftArm"] = "L_Upperarm", ["LeftForeArm"] = "L_Forearm", ["LeftHand"] = "L_Hand",
            ["RightShoulder"] = "R_Clavicle", ["RightArm"] = "R_Upperarm", ["RightForeArm"] = "R_Forearm", ["RightHand"] = "R_Hand",
            ["LeftUpLeg"] = "L_Thigh", ["LeftLeg"] = "L_Calf", ["LeftFoot"] = "L_Foot", ["LeftToeBase"] = "L_ToeBase",
            ["RightUpLeg"] = "R_Thigh", ["RightLeg"] = "R_Calf", ["RightFoot"] = "R_Foot", ["RightToeBase"] = "R_ToeBase",
        };

        static Transform FindBone(Transform[] bones, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var found = bones.FirstOrDefault(t => Canon(t.name) == name);
            if (!found && CcBones.TryGetValue(name, out var cc)) found = bones.FirstOrDefault(t => Canon(t.name) == cc);
            return found;
        }

        /// <summary>
        /// The shelter's suit on a Character Creator body: torso, arms (with the hands: gloves) and legs (with the feet:
        /// boots) in a dark padded thermal cloth; the head, hair and eyes left as they are; the underwear hidden.
        /// </summary>
        static void Suit(GameObject made, Color cloth, string tag)
        {
            var fabric = MakeTexture("Shelter Suit Cloth", 512, false, SuitCloth);
            foreach (var r in made.GetComponentsInChildren<Renderer>(true).ToList())
            {
                if (r.name.StartsWith("Icosphere")) { Object.DestroyImmediate(r.gameObject); continue; }
                var materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (!m) continue;
                    if (m.name.StartsWith("Boxers")) { r.enabled = false; break; }
                    if (m.name.StartsWith("Std_Skin_Body") || m.name.StartsWith("Std_Skin_Arm") || m.name.StartsWith("Std_Skin_Leg") || m.name.StartsWith("Std_Nails"))
                    {
                        string part = m.name.Replace("_Pbr", "").Replace("Std_", "");
                        materials[i] = Art.Variant(m, $"Suit {tag} {part}", v =>
                        {
                            v.SetColor("baseColorFactor", cloth);
                            v.SetTexture("baseColorTexture", fabric);
                            v.SetTexture("normalTexture", null);
                            v.SetTexture("metallicRoughnessTexture", null);
                            v.SetFloat("metallicFactor", 0.08f);
                            v.SetFloat("roughnessFactor", 0.62f);
                        });
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = materials;
            }
        }

        /// <summary>Padded thermal cloth: a fine weave, quilted in soft ridges, a little uneven.</summary>
        static Color SuitCloth(float u, float v)
        {
            float weave = 0.5f + 0.5f * Mathf.Sin(u * 512f * 1.2f) * Mathf.Sin(v * 512f * 1.2f);
            float quilt = Mathf.Pow(Mathf.Abs(Mathf.Sin(v * Mathf.PI * 22f)), 0.35f);
            float blotch = Mathf.PerlinNoise(u * 9f, v * 9f);
            float k = 0.78f + 0.1f * weave + 0.12f * quilt - 0.08f * blotch;
            return new Color(k, k, k, 1f);
        }

        static GameObject sleeperPrefab;
        static float sleeperFoot, sleeperHeight;
        static Vector3 sleeperForward, sleeperCentre;

        /// <summary>
        /// One sleeper, baked: the suited man posed at rest (arms down by the sides, hands a little forward, head bowed),
        /// every skinned part baked into a still mesh, saved as a prefab once per build. Returns null if the model is missing.
        /// </summary>
        static GameObject Sleeper()
        {
            if (sleeperPrefab) return sleeperPrefab;
            var model = Art.Sketchfab("CryoMan");
            if (!model) return null;
            var made = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(made, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            made.transform.position = Vector3.zero;
            foreach (var a in made.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            foreach (var a in made.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            Suit(made, new Color(0.32f, 0.36f, 0.42f), "Sleeper");
            var bones = made.GetComponentsInChildren<Transform>(true);
            Transform B(string n) => FindBone(bones, n);
            Vector3 forward = B("LeftToeBase").position - B("LeftFoot").position;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            void Aim(Transform bone, Transform toward, Vector3 direction)
            {
                if (!bone || !toward) return;
                Vector3 now = toward.position - bone.position;
                bone.rotation = Quaternion.FromToRotation(now, direction) * bone.rotation;
            }
            foreach (var side in new[] { "Left", "Right" })
            {
                Vector3 outward = B(side + "Arm").position - B("Spine2").position;
                outward.y = 0f;
                outward = Vector3.ProjectOnPlane(outward, forward).normalized;
                Aim(B(side + "Arm"), B(side + "ForeArm"), (-up + outward * 0.16f + forward * 0.04f).normalized);
                Aim(B(side + "ForeArm"), B(side + "Hand"), (-up + outward * 0.06f + forward * 0.2f).normalized);
            }
            var head = B("Head");
            if (head) head.rotation = Quaternion.AngleAxis(12f, right) * head.rotation;
            var neck = B("Neck");
            if (neck) neck.rotation = Quaternion.AngleAxis(5f, right) * neck.rotation;

            // Measured by the skeleton, as Dress does: toes to the top of the head.
            float toes = Mathf.Min(B("LeftToeBase").position.y, B("RightToeBase").position.y) - 0.04f * Scale(B);
            float top = B("Head").position.y + 0.24f * Scale(B);
            sleeperFoot = toes;
            sleeperHeight = top - toes;
            sleeperForward = forward;
            Vector3 hips = B("Hips").position;
            sleeperCentre = new Vector3(hips.x, 0f, hips.z);

            var root = new GameObject("Sleeper");
            string folder = $"{Root}/Art/Generated";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder($"{Root}/Art", "Generated");
            int n = 0;
            foreach (var s in made.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!s.enabled || !s.sharedMesh) continue;
                var mesh = new Mesh { name = "Sleeper " + s.name };
                s.BakeMesh(mesh, false);
                mesh.RecalculateBounds();
                string path = $"{folder}/Sleeper {n++}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var part = new GameObject(s.name);
                part.transform.SetParent(root.transform, false);
                part.transform.position = s.transform.position;
                part.transform.rotation = s.transform.rotation;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterials = s.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Object.DestroyImmediate(made);
            sleeperPrefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/Sleeper.prefab");
            Object.DestroyImmediate(root);
            return sleeperPrefab;
        }

        /// <summary>How big the model's units are: the distance from hips to head, as a fraction of a person's (about 0.7 m).</summary>
        static float Scale(System.Func<string, Transform> bone) => Mathf.Max(0.001f, (bone("Head").position - bone("Hips").position).magnitude / 0.72f);

        /// <summary>
        /// A sleeper standing in a pod where the pod model's own figure stood (which is hidden): feet on its floor,
        /// facing out through the glass, a little taller or shorter than the next.
        /// </summary>
        static GameObject PutSleeper(Transform pod, Bounds body, int seed)
        {
            var prefab = Sleeper();
            if (!prefab) return null;
            var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            made.transform.SetParent(pod, false);
            float height = Mathf.Min(body.size.y * 0.97f, 1.62f + (seed * 37 % 17) / 100f);
            float scale = height / Mathf.Max(0.001f, sleeperHeight);
            made.transform.localScale = Vector3.one * scale;
            made.transform.localRotation = Quaternion.FromToRotation(sleeperForward, Vector3.forward);
            Vector3 centre = made.transform.localRotation * (sleeperCentre * scale);
            made.transform.localPosition = new Vector3(body.center.x - centre.x, body.min.y - sleeperFoot * scale, body.center.z - centre.z);
            return made;
        }
            /// <summary>Where a skinned figure stands, in another transform's space: its posed mesh, baked and measured.</summary>
        static Bounds BodyBox(Renderer r, Transform space)
        {
            if (r is SkinnedMeshRenderer s && s.sharedMesh)
            {
                var mesh = new Mesh();
                s.BakeMesh(mesh, false);
                var verts = mesh.vertices;
                Object.DestroyImmediate(mesh);
                if (verts.Length > 0)
                {
                    Vector3 Local(Vector3 v) => space.InverseTransformPoint(s.transform.position + s.transform.rotation * v);
                    var bounds = new Bounds(Local(verts[0]), Vector3.zero);
                    foreach (var v in verts) bounds.Encapsulate(Local(v));
                    return bounds;
                }
            }
            return Art.Measure(r.gameObject, space);
        }
    }
}
