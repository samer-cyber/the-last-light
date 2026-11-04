using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Building
{
    public static partial class LastLightSceneBuilder
    {
        /// <summary>
        /// The survivor's look: a rigged model (now Sketchfab "Man" by Pumpkin, CC BY, in the shelter's suit)
        /// standing in the jointed figure's place. The figure's shapes go; its
        /// joints stay for <see cref="TravellerBody"/> to turn, and the model's skeleton follows them
        /// (<see cref="RigFollow"/>), so walking, running, carrying, tools and the turned head all still work.
        /// </summary>
        static void Dress(Figure f, Transform hand, int layer, string modelName, float height)
        {
            var model = Art.Sketchfab(modelName);
            string[] shapes = { "Pelvis", "Waist", "Ribs", "Neck", "Head", "Hood", "Satchel", "Shoulder", "Arm", "Elbow", "Forearm", "Hand", "Thigh", "Knee", "Shin", "Foot", "Cloak" };
            foreach (var r in f.Body.GetComponentsInChildren<MeshRenderer>(true).ToList())
                if (shapes.Contains(r.name)) Object.DestroyImmediate(r.gameObject);

            // Sized and stood on the ground by its skeleton (toes to head): the skin's import scale can't be trusted.
            var made = (GameObject)PrefabUtility.InstantiatePrefab(model);
            made.name = "Survivor Look";
            var own = made.transform.localRotation;
            made.transform.SetParent(f.Body, false);
            made.transform.localPosition = Vector3.zero;
            // Its own turn kept: the import stands it up facing the way the body walks.
            made.transform.localRotation = own;
            made.transform.localScale = Vector3.one;
            // Bones by their plain names ("mixamorig:LeftForeArm_010" is LeftForeArm).
            var bones = made.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => FindBone(bones, name);
            if (modelName == "CryoMan") Suit(made, new Color(0.30f, 0.34f, 0.40f), "Survivor");
            // From the toes' joints to the head's joint is the height less about 4 cm under the toes and 20 cm of skull
            // (the "_End" leaf bones can't be used: re-exported, they all sit at one point).
            float Toes() => Mathf.Min(Bone("LeftToeBase").position.y, Bone("RightToeBase").position.y);
            made.transform.localScale = Vector3.one * ((height - 0.24f) / Mathf.Max(0.001f, Bone("Head").position.y - Toes()));
            Vector3 hips = Bone("Hips").position;
            made.transform.position += new Vector3(f.Body.position.x - hips.x, f.Body.position.y - (Toes() - 0.04f), f.Body.position.z - hips.z);
            foreach (var r in made.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (var a in made.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            foreach (var a in made.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var t in made.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var s in made.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;

            var foot = new Vector3(0f, -0.45f, 1f);
            var links = new (Transform joint, string bone, string next, Vector3 rest)[]
            {
                (f.Hips, "Hips", "Spine", Vector3.up),
                (f.Spine, "Spine", "Spine1", Vector3.up),
                (f.Chest, "Spine2", "Neck", Vector3.up),
                (f.Neck, "Neck", "Head", Vector3.up),
                (f.Head, "Head", "", Vector3.zero),
                (f.Left.upperArm, "LeftArm", "LeftForeArm", Vector3.down),
                (f.Left.forearm, "LeftForeArm", "LeftHand", Vector3.down),
                (f.Right.upperArm, "RightArm", "RightForeArm", Vector3.down),
                (f.Right.forearm, "RightForeArm", "RightHand", Vector3.down),
                (f.Left.thigh, "LeftUpLeg", "LeftLeg", Vector3.down),
                (f.Left.shin, "LeftLeg", "LeftFoot", Vector3.down),
                (f.Left.foot, "LeftFoot", "LeftToeBase", foot),
                (f.Right.thigh, "RightUpLeg", "RightLeg", Vector3.down),
                (f.Right.shin, "RightLeg", "RightFoot", Vector3.down),
                (f.Right.foot, "RightFoot", "RightToeBase", foot),
            };
            var missing = links.Where(l => !Bone(l.bone) || l.next.Length > 0 && !Bone(l.next)).Select(l => !Bone(l.bone) ? l.bone : l.next).ToList();
            if (missing.Count > 0) Debug.LogWarning($"{modelName}: no bone named {string.Join(", ", missing)}");

            var rig = made.AddComponent<RigFollow>();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("body").objectReferenceValue = f.Body;
            var list = serialized.FindProperty("links");
            list.arraySize = links.Length;
            for (int i = 0; i < links.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("joint").objectReferenceValue = links[i].joint;
                element.FindPropertyRelative("bone").objectReferenceValue = Bone(links[i].bone);
                element.FindPropertyRelative("next").objectReferenceValue = Bone(links[i].next);
                element.FindPropertyRelative("rest").vector3Value = links[i].rest;
            }
            serialized.FindProperty("hipsJoint").objectReferenceValue = f.Hips;
            serialized.FindProperty("hipsBone").objectReferenceValue = Bone("Hips");
            serialized.FindProperty("hand").objectReferenceValue = hand;
            serialized.FindProperty("handBone").objectReferenceValue = Bone("RightHand");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
