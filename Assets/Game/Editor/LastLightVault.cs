using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.Building.SceneKit;

namespace Game.Building
{
    public static partial class LastLightSceneBuilder
    {
        public struct VaultParts
        {
            public Light Light;
            public Transform Door, Lid;
            public Vector3 PodStand, PodFacing;
            public Collider Hall;
            /// <summary>The survivor's pod panel, which shrieks when they wake, and its alarm.</summary>
            public Touchable Panel;
            public AudioSource Alarm;
            /// <summary>Mr. Okafor's pod, which fails while the survivor watches.</summary>
            public PodLights Pod9;
            /// <summary>Ice two centuries thick round the blast door, holding it in its frame.</summary>
            public Frozen DoorIce;
        }

        const float HallLength = 30f, HallWidth = 9f, HallHeight = 4.5f, PodPitch = 1.3f;

        // How each sleeper's pod stands (script, part 1): thirty-one still green, three amber (Mr. Okafor's,
        // pod 9, among them), two red, three dark.
        static readonly int[] Amber = { 9, 26, 35 }, Red = { 17, 33 }, Dark = { 4, 22, 30 };

        /// <summary>
        /// The vault, dug into its hill: a round door in a concrete face, and behind it the sleeping hall,
        /// 30 m long, built from real materials and props (Art): bare concrete with a painted band, steel
        /// beams across the ceiling, a duct, pipes and cable trays, strip lights mostly dead and three
        /// still burning cold, red emergency lamps at the far end. Forty pods stand in alcoves along the
        /// walls, 1 to 20 on the west, 21 to 39 on the east; 11 and 12 are Ima's and her mother's. The
        /// survivor's pod, 40, stands at the far end with its own panel. By the door: a maintenance desk
        /// with a dead terminal, shelves, and Spark's nest in the first alcove on the east.
        /// </summary>
        static VaultParts MakeVault(Transform parent, Mesh cube, Mesh cylinder, Mesh sphere, Material concrete, Material metal, Material window,
            Material glass, Material frost, Material light, Material ice, Material steamMaterial, List<Landmark> landmarks)
        {
            var parts = new VaultParts();
            var root = new GameObject("Vault").transform;
            root.SetParent(parent, false);
            float y = Height(0f, VaultDoorZ + 8f);
            root.position = new Vector3(0f, y, VaultDoorZ);

            // Grey concrete everywhere, a tread-plate walk down the middle, dark steel, old teal paint, worn plates.
            var floor = Art.Surface("Vault Floor", "ambientCG/Concrete031", 2.5f, new Color(0.78f, 0.8f, 0.84f));
            var wall = Art.Surface("Vault Wall", "ambientCG/Concrete031", 2.5f);
            var band = Art.Surface("Vault Wall Low", "ambientCG/PaintedMetal006", 1.6f, new Color(0.62f, 0.74f, 0.72f));
            var steel = Art.Surface("Vault Steel", "ambientCG/Metal027", 1.5f);
            var painted = Art.Surface("Vault Painted", "ambientCG/PaintedMetal006", 3f, new Color(0.6f, 0.68f, 0.68f));
            var tread = Art.Surface("Vault Tread", "ambientCG/DiamondPlate008C", 0.8f);
            var rust = Art.Surface("Vault Rust", "PolyHaven/rusty_metal_03", 2f);
            var worn = Art.Surface("Vault Worn", "PolyHaven/metal_plate_02", 1.5f);
            // Pod glass, frosted, and rime grown in from its edges, thickest at the bottom.
            var podGlass = MakeLit("Pod Glass Frosted", new Color(0.55f, 0.64f, 0.7f, 0.32f), 0.6f, 0f);
            Transparent(podGlass);
            var rimeLook = MakeLit("Pod Rime", Color.white, 0.5f, 0f);
            rimeLook.SetTexture("_BaseMap", MakeTexture("Pod Rime", 256, true, Rime));
            Transparent(rimeLook);
            var green = Glow("Pod Light Green", new Color(0.25f, 1f, 0.45f), 2.2f);
            var amber = Glow("Pod Light Amber", new Color(1f, 0.55f, 0.1f), 2.2f);
            var red = Glow("Pod Light Red", new Color(1f, 0.1f, 0.06f), 2.6f);
            var dead = Glow("Pod Light Dead", new Color(0.05f, 0.05f, 0.05f), 0f);
            var sleeperLook = MakeLit("Sleeper", new Color(0.06f, 0.07f, 0.09f), 0.2f, 0f);

            Renderer Box(string name, Transform under, Material m, Vector3 at, Vector3 size, bool solid = true)
            {
                var part = MakePart(name, under, cube, m, 1f);
                part.transform.localPosition = at;
                part.transform.localScale = size;
                part.shadowCastingMode = ShadowCastingMode.On;
                part.receiveShadows = true;
                if (solid) part.gameObject.AddComponent<BoxCollider>();
                return part;
            }
            // A painted sign (Textures/Signs, from tools/make_signs.py): a thin plate, its front facing the plate's forward.
            Renderer Decal(string name, Transform under, string sign, Vector3 at, Vector2 size, Quaternion turn)
            {
                var look = MakeLit("Sign " + sign, Color.white, 0.15f, 0f);
                look.SetTexture("_BaseMap", Load<Texture2D>($"{Root}/Textures/Signs/{sign}.png"));
                Transparent(look);
                var plate = Box(name, under, look, at, new Vector3(size.x, size.y, 0.004f), false);
                plate.transform.localRotation = turn;
                plate.shadowCastingMode = ShadowCastingMode.Off;
                return plate;
            }
            // A hand wheel on a door face: a hub, four spokes, a rim of eight short bars.
            void Wheel(Transform under, Vector3 at, float face, Material m)
            {
                var wheel = new GameObject("Door Wheel").transform;
                wheel.SetParent(under, false);
                wheel.localPosition = at;
                var hub = MakePart("Wheel Hub", wheel, cylinder, m, 1f);
                hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hub.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
                hub.shadowCastingMode = ShadowCastingMode.On;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    Art.Slab("Wheel Rim", wheel, m, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, 0.42f, face * 0.03f), new Vector3(0.34f, 0.06f, 0.06f), false, Quaternion.Euler(0f, 0f, a));
                    if (i % 2 == 0)
                        Art.Slab("Wheel Spoke", wheel, m, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, 0.21f, face * 0.02f), new Vector3(0.045f, 0.4f, 0.04f), false, Quaternion.Euler(0f, 0f, a));
                }
            }

            // The way in: a concrete portal in the hill's face (jambs and a lintel standing out under a steel hood,
            // wing walls splayed to hold the slope back) and a steel blast door in the opening, 2.8 m by 3.2 m.
            const float door = 2.8f, doorHigh = 3.2f;
            Art.Slab("Face Left", root, wall, new Vector3(-(door * 0.5f + 2.5f), 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Art.Slab("Face Right", root, wall, new Vector3(door * 0.5f + 2.5f, 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Art.Slab("Face Top", root, wall, new Vector3(0f, doorHigh + 1.9f, -2.5f), new Vector3(door + 0.4f, 3.8f, 5f));
            foreach (float side in new[] { -1f, 1f })
            {
                Art.Slab("Jamb", root, wall, new Vector3(side * (door * 0.5f + 0.35f), (doorHigh + 0.6f) * 0.5f, 0.4f), new Vector3(0.7f, doorHigh + 0.6f, 0.8f));
                Art.Slab("Wing Wall", root, wall, new Vector3(side * (door * 0.5f + 1.6f), 1.05f, 0.95f), new Vector3(2.7f, 2.1f, 0.45f), turn: Quaternion.Euler(0f, -side * 46f, 0f));
            }
            Art.Slab("Lintel", root, wall, new Vector3(0f, doorHigh + 0.3f, 0.4f), new Vector3(door + 1.4f, 0.6f, 0.8f));
            // The passage through the hill from the door to the hall: a floor under it (the walls and roof are the face).
            Art.Slab("Passage Floor", root, floor, new Vector3(0f, -0.25f, -2.45f), new Vector3(door + 0.6f, 0.5f, 5.3f));
            Art.Slab("Passage Tread", root, tread, new Vector3(0f, 0.01f, -2.6f), new Vector3(1.6f, 0.02f, 4.6f), false);
            Art.Slab("Hood", root, steel, new Vector3(0f, doorHigh + 0.66f, 0.7f), new Vector3(door + 2f, 0.12f, 1.5f));
            Decal("Sign Shelter 7", root, "Shelter 7", new Vector3(0f, doorHigh + 0.3f, 0.805f), new Vector2(door + 0.9f, (door + 0.9f) * 320f / 1024f), Quaternion.identity);
            Art.Prop("industrial_caged_sconce", root, new Vector3(door * 0.5f + 0.35f, doorHigh - 0.35f, 0.8f), Quaternion.identity, "industrial_caged_sconce_a", false);
            // Its lamp still burns, just: a dim warm light over the door that flickers.
            var porch = new GameObject("Door Lamp").AddComponent<Light>();
            porch.transform.SetParent(root, false);
            porch.transform.localPosition = new Vector3(door * 0.5f + 0.35f, doorHigh - 0.2f, 1.15f);
            porch.type = LightType.Point;
            porch.color = new Color(1f, 0.72f, 0.42f);
            porch.range = 7f;
            porch.intensity = 1.3f;
            porch.shadows = LightShadows.Soft;
            porch.gameObject.AddComponent<Flicker>();
            // The blast door turns inward on its left edge and lies against the passage wall when open.
            var hinge = new GameObject("Door Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(-door * 0.5f + 0.17f, doorHigh * 0.5f, -0.35f);
            var leaf = new GameObject("Door").transform;
            leaf.SetParent(hinge, false);
            float leafWide = door - 0.14f;
            leaf.localPosition = new Vector3(leafWide * 0.5f, 0f, 0f);
            Art.Slab("Door Leaf", leaf, steel, Vector3.zero, new Vector3(leafWide, doorHigh - 0.04f, 0.3f));
            foreach (float face in new[] { -1f, 1f })
            {
                float z = face * 0.17f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Art.Slab("Door Stile", leaf, worn, new Vector3(side * (leafWide * 0.5f - 0.12f), 0f, z), new Vector3(0.2f, doorHigh - 0.12f, 0.05f), false);
                    Art.Slab("Door Rail", leaf, worn, new Vector3(0f, side * (doorHigh * 0.5f - 0.14f), z), new Vector3(leafWide - 0.1f, 0.2f, 0.05f), false);
                    Art.Slab("Door Rib", leaf, worn, new Vector3(0f, side * 0.55f, z), new Vector3(leafWide - 0.45f, 0.12f, 0.04f), false);
                }
                Art.Slab("Door Stripes", leaf, Hazard(), new Vector3(0f, -doorHigh * 0.5f + 0.38f, face * 0.152f), new Vector3(leafWide - 0.45f, 0.24f, 0.01f), false);
                Wheel(leaf, new Vector3(0f, 0f, face * 0.2f), face, steel);
            }
            foreach (float high in new[] { -1.05f, 1.05f })
            {
                var knuckle = MakePart("Door Knuckle", hinge, cylinder, steel, 1f);
                knuckle.transform.localPosition = new Vector3(-0.02f, high, 0.2f);
                knuckle.transform.localScale = new Vector3(0.17f, 0.24f, 0.17f);
                knuckle.shadowCastingMode = ShadowCastingMode.On;
            }
            parts.Door = hinge;

            // On the hall side the door is frozen into its frame: a seal of ice all round its edge, a drift of it
            // along the bottom, icicles from the top. Heat melts it (a fire beside it, a torch), a pick breaks it.
            var doorIce = new GameObject("Door Ice").transform;
            doorIce.SetParent(root, false);
            doorIce.localPosition = new Vector3(0f, 0f, -0.62f);
            var drift = Art.Slab("Ice Drift", doorIce, ice, new Vector3(0f, 0.32f, -0.08f), new Vector3(door + 0.1f, 0.64f, 0.42f));
            drift.shadowCastingMode = ShadowCastingMode.On;
            foreach (float side in new[] { -1f, 1f })
                Art.Slab("Ice Seal", drift.transform, ice, new Vector3(side * (door * 0.5f - 0.1f), 1.75f - 0.32f, 0.08f), new Vector3(0.36f, doorHigh - 0.3f, 0.24f), false,
                    Quaternion.Euler(0f, 0f, side * 0.6f));
            Art.Slab("Ice Seal Top", drift.transform, ice, new Vector3(0f, doorHigh - 0.12f - 0.32f, 0.08f), new Vector3(door + 0.1f, 0.34f, 0.26f), false);
            var icicles = new System.Random(5);
            for (float x = -door * 0.5f + 0.3f; x < door * 0.5f - 0.25f; x += 0.17f + (float)icicles.NextDouble() * 0.2f)
            {
                float hang = 0.18f + (float)icicles.NextDouble() * 0.55f;
                Art.Slab("Icicle", drift.transform, ice, new Vector3(x, doorHigh - 0.3f - hang * 0.5f - 0.32f, 0.12f), new Vector3(0.05f + hang * 0.04f, hang, 0.05f), false,
                    Quaternion.Euler(0f, (float)icicles.NextDouble() * 90f, 0f));
            }
            // An unseen sheet over the door's face, part of the ice: aiming anywhere at the door is aiming at its ice.
            var sheet = new GameObject("Ice Sheet").AddComponent<BoxCollider>();
            sheet.transform.SetParent(drift.transform, false);
            sheet.transform.localPosition = new Vector3(0f, doorHigh * 0.5f - 0.32f, 0.17f);
            sheet.size = new Vector3(door - 0.2f, doorHigh - 0.2f, 0.06f);
            var frozenDoor = doorIce.gameObject.AddComponent<Frozen>();
            var (doorDrips, doorSteam, doorCrack) = IceEffects(doorIce, new Vector3(0f, 1.6f, -0.2f), 1.2f, steamMaterial);
            Wire(frozenDoor, ("shell", drift), ("drips", doorDrips), ("steam", doorSteam), ("sound", doorCrack),
                ("crackClip", FindClip("ice_crack")), ("breakClip", FindClip("ice_break_long") ?? FindClip("ice_break")), ("meltClip", FindClip("ice_melt_loop")));
            SetValue(frozenDoor, "needs", 11f);
            SetValue(frozenDoor, "blows", 5);
            parts.DoorIce = frozenDoor;
            landmarks.Add(MakeLandmark("Door Ice Place", doorIce, doorIce.position + Vector3.up * 1.5f + Vector3.back * 0.1f, "VAULT_DOOR", "the vault door",
                "the steel blast door out of the vault, from inside. Two hundred years of ice hold it in its frame: you have tried for years and you are too small to shift it. Heat would melt the ice (a fire beside it, a torch held to it) and a hard blow would break it",
                new Vector3(door + 0.4f, doorHigh, 0.8f)));

            // The hall behind it, inside the hill: z runs from 0 at the door wall to -30 at the far end.
            var hall = new GameObject("Hall").transform;
            hall.SetParent(root, false);
            hall.localPosition = new Vector3(0f, 0f, -5f);
            float half = HallWidth * 0.5f, mid = -HallLength * 0.5f;
            Art.Slab("Floor", hall, floor, new Vector3(0f, -0.25f, mid), new Vector3(HallWidth + 1f, 0.5f, HallLength + 1f));
            Art.Slab("Walkway", hall, tread, new Vector3(0f, 0.01f, mid - 0.4f), new Vector3(1.6f, 0.02f, HallLength - 2.6f), false);
            foreach (float side in new[] { -1f, 1f })
                Art.Slab("Walkway Edge", hall, steel, new Vector3(side * 0.82f, 0.015f, mid - 0.4f), new Vector3(0.04f, 0.03f, HallLength - 2.6f), false);
            Art.Slab("Ceiling", hall, wall, new Vector3(0f, HallHeight + 0.25f, mid), new Vector3(HallWidth + 1f, 0.5f, HallLength + 1f));
            foreach (float side in new[] { -1f, 1f })
                Art.Slab(side < 0 ? "Wall West" : "Wall East", hall, wall, new Vector3(side * (half + 0.25f), HallHeight * 0.5f, mid), new Vector3(0.5f, HallHeight, HallLength + 1f));
            Art.Slab("Wall End", hall, wall, new Vector3(0f, HallHeight * 0.5f, -HallLength - 0.25f), new Vector3(HallWidth + 1f, HallHeight, 0.5f));
            // A painted band, worn and peeling, low on the walls that show: the end and either side of the door.
            Art.Slab("Band End", hall, band, new Vector3(0f, 0.6f, -HallLength + 0.02f), new Vector3(HallWidth, 1.2f, 0.04f), false);
            foreach (float side in new[] { -1f, 1f })
                Art.Slab("Band Door", hall, band, new Vector3(side * (door * 0.5f + (half - door * 0.5f) * 0.5f), 0.6f, -0.02f), new Vector3(half - door * 0.5f, 1.2f, 0.04f), false);

            // Alcoves along both walls: concrete piers between the pods, a painted beam over them,
            // then pipes on the east and a duct along the ceiling, cable trays above the beams.
            int alcoves = 20;
            float firstPier = -0.45f - PodPitch, depth = 0.85f;
            for (int i = 0; i <= alcoves; i++)
                foreach (float side in new[] { -1f, 1f })
                    Art.Slab("Pier", hall, wall, new Vector3(side * (half - depth * 0.5f), HallHeight * 0.5f, firstPier - i * PodPitch), new Vector3(depth, HallHeight, 0.2f));
            float runFrom = firstPier, runTo = firstPier - alcoves * PodPitch, runMid = (runFrom + runTo) * 0.5f, run = runFrom - runTo;
            foreach (float side in new[] { -1f, 1f })
            {
                Art.Slab("Pod Beam", hall, steel, new Vector3(side * (half - depth * 0.5f), 3.12f, runMid), new Vector3(depth, 0.24f, run + 0.2f), false);
                Art.Slab("Cable Tray", hall, worn, new Vector3(side * (half - 0.3f), 3.92f, runMid), new Vector3(0.36f, 0.05f, run + 0.2f), false);
                // A pair of heavy cables along each tray, in pieces from the kit at twice their size, side by side.
                for (float z = runTo; z < runFrom - 1.2f; z += 1.22f)
                    Art.Prop("modular_electric_cables", hall, new Vector3(side * (half - 0.3f), 3.99f, z), Quaternion.LookRotation(Vector3.right, Vector3.forward), "cable_double_alt_long", false, 2f);
            }
            // A pipe along the east wall over the pods, with a valve every few lengths, its wheel to the hall.
            for (float z = runFrom - 1f; z > runTo + 0.5f; z -= 1.95f)
                Art.Prop("modular_industrial_pipes_01", hall, new Vector3(half - 0.22f, 3.55f, z), Quaternion.Euler(90f, 0f, 0f), "modular_industrial_pipes_01_pipe02", false);
            for (float z = runFrom - 1f - 1.95f * 2f; z > runTo + 2f; z -= 1.95f * 4f)
                Art.Prop("modular_industrial_pipes_01", hall, new Vector3(half - 0.22f, 3.55f, z - 0.97f), Quaternion.LookRotation(Vector3.left, Vector3.forward), "modular_industrial_pipes_01_pipe08", false);
            // Stencilled signs: which pods are where, the door, the hall's name and Lumen's mark over the far end.
            Decal("Sign Pods West", hall, "Pods West", new Vector3(-(half - depth) + 0.005f, 3.12f, -4f), new Vector2(1.2f, 0.23f), Quaternion.Euler(0f, 90f, 0f));
            Decal("Sign Pods East", hall, "Pods East", new Vector3(half - depth - 0.005f, 3.12f, -4f), new Vector2(1.2f, 0.23f), Quaternion.Euler(0f, -90f, 0f));
            Decal("Sign Keep Clear", hall, "Keep Clear", new Vector3(0f, doorHigh + 0.55f, -0.03f), new Vector2(2.4f, 0.7f), Quaternion.Euler(0f, 180f, 0f));
            Decal("Sign Hall A", hall, "Hall A", new Vector3(0f, 3.85f, -HallLength + 0.03f), new Vector2(1.8f, 0.35f), Quaternion.identity);
            Decal("Lumen Mark", hall, "Lumen Mark", new Vector3(-2.2f, 3.5f, -HallLength + 0.03f), new Vector2(0.7f, 0.7f), Quaternion.identity);
            // Steel beams across the ceiling every three metres.
            for (float z = -1.5f; z > -HallLength; z -= 3f)
            {
                Art.Slab("Beam", hall, steel, new Vector3(0f, HallHeight - 0.3f, z), new Vector3(HallWidth, 0.04f, 0.3f), false);
                Art.Slab("Beam Web", hall, steel, new Vector3(0f, HallHeight - 0.15f, z), new Vector3(HallWidth, 0.26f, 0.04f), false);
            }
            // The air duct, hung under the beams, ending in a dead fan on the far wall.
            const float ductX = -1.9f, ductY = 3.95f;
            int segment = 0;
            for (float z = -HallLength + 0.15f; z < -1.5f; z += 1.42f, segment++)
            {
                Art.Prop("modular_airduct_circular_01", hall, new Vector3(ductX, ductY, z), Quaternion.identity, "modular_airduct_circular_triple", false);
                if (segment % 2 == 1)
                    Art.Prop("modular_airduct_circular_01", hall, new Vector3(ductX, ductY, z), Quaternion.identity, "modular_airduct_circular_brace", false);
            }
            Art.Prop("modular_airduct_circular_01", hall, new Vector3(ductX, ductY, -HallLength + 0.02f), Quaternion.identity, "modular_airduct_circular_fan", false);

            // Strip lights down the middle between the beams: most dead, three still burning cold.
            var tubeLit = new Dictionary<string, Material>();
            var tubeDead = new Dictionary<string, Material>();
            foreach (var m in Art.Model("mounted_fluorescent_lights").GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
            {
                bool glows = m.GetTexture("emissiveTexture") != null || m.name.Contains("glass");
                tubeLit[m.name] = glows ? Art.Variant(m, "Tube Lit " + m.name, v => v.SetColor("emissiveFactor", new Color(0.55f, 0.75f, 1f) * 3f)) : m;
                tubeDead[m.name] = Art.Variant(m, "Tube Dead " + m.name, v => v.SetColor("emissiveFactor", Color.black));
            }
            var burning = new[] { -3f, -12f, -21f, -27f };
            for (float z = -3f; z > -HallLength; z -= 3f)
            {
                bool on = burning.Contains(z);
                foreach (float lane in new[] { -0.16f, 0.16f })
                {
                    var tube = Art.Prop("mounted_fluorescent_lights", hall, new Vector3(lane, HallHeight - 0.005f, z), Quaternion.Euler(0f, 90f, 0f), "mounted_fluorescent_lights_d", false);
                    foreach (var r in tube.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = r.sharedMaterials.Select(m => m && (on ? tubeLit : tubeDead).TryGetValue(m.name, out var v) ? v : m).ToArray();
                }
                if (!on) continue;
                var lamp = new GameObject("Hall Lamp").AddComponent<Light>();
                lamp.transform.SetParent(hall, false);
                lamp.transform.localPosition = new Vector3(0f, HallHeight - 0.35f, z);
                lamp.type = LightType.Point;
                lamp.color = new Color(0.65f, 0.8f, 1f);
                lamp.range = 10f;
                lamp.intensity = 1.8f;
                lamp.shadows = LightShadows.Soft;
                lamp.gameObject.AddComponent<Flicker>();
            }
            // Red emergency lamps high in the far corners, away from the survivor's pod (and Spark, who waits by it).
            foreach (float side in new[] { -1f, 1f })
            {
                Art.Prop("industrial_caged_sconce", hall, new Vector3(side * 3.6f, 3.35f, -HallLength + 0.01f), Quaternion.identity, "industrial_caged_sconce_a", false);
                var alarm = new GameObject("Emergency Lamp").AddComponent<Light>();
                alarm.transform.SetParent(hall, false);
                alarm.transform.localPosition = new Vector3(side * 3.6f, 3.45f, -HallLength + 0.45f);
                alarm.type = LightType.Point;
                alarm.color = new Color(1f, 0.16f, 0.1f);
                alarm.range = 4f;
                alarm.intensity = 0.55f;
            }

            var region = new GameObject("Hall Region").AddComponent<BoxCollider>();
            region.transform.SetParent(hall, false);
            region.isTrigger = true;
            region.center = new Vector3(0f, HallHeight * 0.5f, mid);
            region.size = new Vector3(HallWidth, HallHeight, HallLength);
            region.gameObject.layer = 2;
            region.gameObject.AddComponent<Indoors>();
            parts.Hall = region;
            // Metal and glass in here reflect the hall, not the bright sky outside: a probe, baked at the end.
            var probe = new GameObject("Hall Reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(hall, false);
            probe.transform.localPosition = new Vector3(0f, 1.8f, mid);
            probe.size = new Vector3(HallWidth, HallHeight, HallLength);
            probe.center = new Vector3(0f, HallHeight * 0.5f - 1.8f, 0f);
            probe.mode = ReflectionProbeMode.Custom;
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.intensity = 1f;

            // The pods, one to an alcove: a stasis chamber (Sketchfab, TampaJoey, CC-BY) with its sleeper inside,
            // posed still, behind glass gone cloudy with cold; its lights the colour of the pod's state.
            var chamberModel = Art.Sketchfab("PodChamber");
            var chamberMaterials = chamberModel.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
            var chamberGlow = chamberMaterials.First(m => m.name == "M_glow");
            var chamberGlass = Art.Variant(chamberMaterials.First(m => m.name == "M_glass"), "Chamber Glass Frosted", v =>
            {
                v.SetColor("baseColorFactor", new Color(0.72f, 0.82f, 0.9f, 0.62f));
                v.SetFloat("roughnessFactor", 0.55f);
            });
            Material GlowOf(Material state) => Art.Variant(chamberGlow, "Chamber Glow " + state.name.Replace("Pod Light ", ""), v =>
                v.SetColor("emissiveFactor", state.GetColor("_EmissionColor")));
            var sleepPose = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(chamberModel)).OfType<AnimationClip>().FirstOrDefault();
            PodLights failing = null;
            Transform Pod(int number, float side, float z)
            {
                var pod = new GameObject($"Pod {number}").transform;
                pod.SetParent(hall, false);
                pod.localPosition = new Vector3(side * (half - 0.43f), 0f, z);
                pod.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                var status = Amber.Contains(number) ? amber : Red.Contains(number) ? red : Dark.Contains(number) ? dead : green;
                var chamber = Art.Place(chamberModel, pod, new Vector3(0f, 0f, -0.02f), 2.55f, Quaternion.identity);
                // Asleep: the stillest moment of the model's float, arms down, standing.
                if (sleepPose) sleepPose.SampleAnimation(chamber, 0.45f);
                foreach (var a in chamber.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
                foreach (var a in chamber.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                var glow = GlowOf(status);
                foreach (var r in chamber.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Select(m => m == null ? m : m.name == "M_glow" ? glow : m.name == "M_glass" ? chamberGlass : m).ToArray();
                // A faint light inside, the colour of the pod's state; the dead ones are dark.
                Light inside = null;
                if (status != dead)
                {
                    inside = new GameObject("Pod Inside").AddComponent<Light>();
                    inside.transform.SetParent(pod, false);
                    inside.transform.localPosition = new Vector3(0f, 2.4f, 0.02f);
                    inside.type = LightType.Point;
                    inside.color = status == green ? new Color(0.55f, 0.92f, 0.95f) : status == amber ? new Color(1f, 0.62f, 0.3f) : new Color(1f, 0.25f, 0.2f);
                    inside.range = 1.7f;
                    inside.intensity = 0.9f;
                    inside.shadows = LightShadows.None;
                }
                // Mr. Okafor's pod fails while the survivor watches (the director calls it).
                if (number == 9)
                {
                    failing = pod.gameObject.AddComponent<PodLights>();
                    var voice = MakeSource("Pod Voice", pod, null, false);
                    voice.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    voice.spatialBlend = 0.85f;
                    voice.minDistance = 2f;
                    voice.maxDistance = 25f;
                    Wire(failing, ("from", glow), ("to", GlowOf(red)), ("inside", inside), ("voice", voice), ("failClip", FindClip("pod_fail")));
                }
                var body = pod.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, 1.48f, -0.02f);
                body.size = new Vector3(1.1f, 2.96f, 0.82f);
                return pod;
            }
            for (int i = 0; i < alcoves; i++)
            {
                float z = firstPier - (i + 0.5f) * PodPitch;
                var west = Pod(i + 1, -1f, z);
                if (i + 1 == 11 || i + 1 == 12)
                    landmarks.Add(MakeLandmark($"Pod {i + 1} Place", west, west.position + Vector3.up * 1.3f, $"POD_{i + 1}", $"pod {i + 1}",
                        i + 1 == 11 ? "pod 11: Ima sleeps in it, the girl who named Spark; its little light still green" : "pod 12: Ima's mother sleeps in it; its little light still green",
                        new Vector3(1.2f, 2.6f, 1.2f)));
                if (i + 1 == 9)
                    landmarks.Add(MakeLandmark("Pod 9 Place", west, west.position + Vector3.up * 1.3f, "POD_9", "pod 9",
                        "pod 9: Mr. Okafor sleeps in it, who taught music at the school", new Vector3(1.2f, 2.6f, 1.2f)));
                if (i > 0) Pod(20 + i, 1f, z);
            }
            parts.Pod9 = failing;

            // The survivor's pod at the far end, bigger, cracked open; its label frosted over; its own panel beside it.
            var mine = new GameObject("Your Pod").transform;
            mine.SetParent(hall, false);
            mine.localPosition = new Vector3(0f, 0f, -HallLength + 0.75f);
            // Its base is a low plate the survivor stands on, inside the pod.
            Art.Slab("Pod Plinth", mine, steel, new Vector3(0f, 0.03f, -0.05f), new Vector3(1.4f, 0.06f, 0.95f), false);
            Art.Slab("Pod Tread", mine, tread, new Vector3(0f, 0.066f, 0f), new Vector3(1.3f, 0.012f, 0.8f), false);
            Art.Slab("Pod Back", mine, worn, new Vector3(0f, 1.5f, -0.45f), new Vector3(1.25f, 2.5f, 0.1f));
            foreach (float post in new[] { -1f, 1f })
                Art.Slab("Pod Post", mine, painted, new Vector3(post * 0.62f, 1.5f, -0.08f), new Vector3(0.14f, 2.5f, 0.8f));
            Art.Slab("Pod Housing", mine, steel, new Vector3(0f, 2.92f, -0.08f), new Vector3(1.4f, 0.36f, 0.95f));
            var lidPivot = new GameObject("Lid").transform;
            lidPivot.SetParent(mine, false);
            lidPivot.localPosition = new Vector3(-0.55f, 1.42f, 0.3f);
            var lidGlass = Box("Lid Glass", lidPivot, podGlass, new Vector3(0.55f, 0f, 0f), new Vector3(1.05f, 2.3f, 0.05f), false);
            lidGlass.shadowCastingMode = ShadowCastingMode.Off;
            var lidFrost = Box("Lid Rime", lidPivot, rimeLook, new Vector3(0.55f, 0f, 0.03f), new Vector3(1.05f, 2.3f, 0.004f), false);
            lidFrost.shadowCastingMode = ShadowCastingMode.Off;
            parts.Lid = lidPivot;
            // The pod's own lamp, still on inside its housing: a cold white light on whoever stands in it.
            var podLamp = new GameObject("Pod Lamp").AddComponent<Light>();
            podLamp.transform.SetParent(mine, false);
            podLamp.transform.localPosition = new Vector3(0f, 2.6f, 0.25f);
            podLamp.type = LightType.Point;
            podLamp.color = new Color(0.75f, 0.88f, 1f);
            podLamp.range = 3.2f;
            podLamp.intensity = 1.1f;
            podLamp.shadows = LightShadows.Soft;
            Art.Slab("Pod Label", mine, worn, new Vector3(0f, 2.92f, 0.4f), new Vector3(0.9f, 0.16f, 0.02f), false);
            // Its own panel: a steel console beside it, the instrument set into its top.
            var console = Art.Slab("Pod Console", mine, steel, new Vector3(1.08f, 0.55f, 0.12f), new Vector3(0.42f, 1.1f, 0.32f));
            Art.Prop("vintage_spacecraft_instrument", console.transform, new Vector3(0f, 0.55f, 0.02f), Quaternion.Euler(-25f, -15f, 0f));
            // It shrieks when the survivor wakes, a red lamp blinking on it, until they touch it.
            var panel = console.gameObject.AddComponent<Touchable>();
            SetValue(panel, "title", "your pod's panel");
            SetValue(panel, "verb", "touch the panel");
            parts.Panel = panel;
            var panelAlarm = MakeSource("Panel Alarm", console.transform, FindClip("pod_alarm_loop"), true);
            panelAlarm.transform.localPosition = new Vector3(0f, 0.6f, 0.1f);
            panelAlarm.spatialBlend = 0.8f;
            panelAlarm.minDistance = 2.5f;
            panelAlarm.maxDistance = 30f;
            panelAlarm.volume = 0.55f;
            parts.Alarm = panelAlarm;
            var alarmLamp = new GameObject("Panel Lamp").AddComponent<Light>();
            alarmLamp.transform.SetParent(console.transform, false);
            alarmLamp.transform.localPosition = new Vector3(0f, 0.75f, 0.25f);
            alarmLamp.type = LightType.Point;
            alarmLamp.color = new Color(1f, 0.2f, 0.12f);
            alarmLamp.range = 1.4f;
            alarmLamp.intensity = 0f;
            alarmLamp.shadows = LightShadows.None;
            landmarks.Add(MakeLandmark("Your Pod Place", mine, mine.position + Vector3.up * 1.3f, "YOUR_POD", "your pod",
                "the pod the survivor woke in, at the end of the hall; its glass cracked open in the cold. Its label is frosted; scraped clean it reads: ARCHITECT. LUMEN MIND SYSTEMS",
                new Vector3(1.4f, 2.6f, 1.2f)));
            parts.PodStand = mine.position + mine.forward * 0.08f;
            parts.PodFacing = mine.forward;
            // Crates in the west corner by it, barrels in the east.
            Art.Prop("old_military_crate", hall, new Vector3(-3.2f, 0f, -HallLength + 1.1f), Quaternion.Euler(0f, 90f, 0f));
            Art.Prop("Barrel_01", hall, new Vector3(3.3f, 0f, -HallLength + 0.6f), Quaternion.Euler(0f, 30f, 0f));
            Art.Prop("Barrel_01", hall, new Vector3(2.75f, 0f, -HallLength + 0.75f), Quaternion.Euler(0f, 160f, 0f));
            Art.Prop("utility_box_01", hall, new Vector3(3.6f, 0f, -HallLength + 1.3f), Quaternion.Euler(0f, -90f, 0f));

            // The maintenance corner by the door: a desk with a dead terminal and a gas mask on a hook; shelves,
            // a power box and an extinguisher on the other side; a camera high in the corner.
            Art.Prop("metal_office_desk", hall, new Vector3(-3.1f, 0f, -0.55f), Quaternion.Euler(0f, 180f, 0f));
            Art.Prop("Television_01", hall, new Vector3(-3.35f, 0.79f, -0.45f), Quaternion.Euler(0f, 180f, 0f));
            Art.Prop("medical_box", hall, new Vector3(-2.55f, 0.79f, -0.6f), Quaternion.Euler(0f, 160f, 0f));
            Art.Prop("old_gas_mask", hall, new Vector3(-1.95f, 1.95f, -0.13f), Quaternion.Euler(0f, 180f, 0f), solid: false);
            Art.Prop("steel_frame_shelves_03", hall, new Vector3(3.15f, 0f, -0.42f), Quaternion.Euler(0f, 180f, 0f));
            Art.Prop("metal_tool_chest", hall, new Vector3(2.75f, 0f, -1.2f), Quaternion.Euler(0f, 200f, 0f));
            Art.Prop("korean_fire_extinguisher_01", hall, new Vector3(1.95f, 0f, -0.25f), Quaternion.Euler(0f, 180f, 0f));
            Art.Prop("power_box_01", hall, new Vector3(2.95f, 2.75f, -0.02f), Quaternion.Euler(0f, 180f, 0f), solid: false);
            Art.Prop("security_camera_02", hall, new Vector3(4.2f, 4.1f, -0.05f), Quaternion.Euler(20f, 200f, 0f), solid: false);

            // Spark's nest: the first alcove on the east, a shelf with what it kept.
            var nest = new GameObject("Nest").transform;
            nest.SetParent(hall, false);
            nest.localPosition = new Vector3(half - 0.45f, 1.1f, firstPier - 0.5f * PodPitch);
            Art.Slab("Nest Shelf", nest, worn, new Vector3(0f, -0.05f, 0f), new Vector3(0.75f, 0.06f, 1.05f), false);
            var duck = MakePart("Duck", nest, sphere, light, 1f);
            duck.transform.localPosition = new Vector3(-0.1f, 0.1f, -0.3f);
            duck.transform.localScale = new Vector3(0.16f, 0.13f, 0.2f);
            var duckHead = MakePart("Duck Head", nest, sphere, light, 1f);
            duckHead.transform.localPosition = new Vector3(-0.1f, 0.22f, -0.22f);
            duckHead.transform.localScale = Vector3.one * 0.09f;
            var cup = MakePart("Teacup", nest, cylinder, frost, 1f);
            cup.transform.localPosition = new Vector3(-0.1f, 0.06f, 0.05f);
            cup.transform.localScale = new Vector3(0.11f, 0.05f, 0.11f);
            Box("Music Box", nest, metal, new Vector3(-0.1f, 0.07f, 0.35f), new Vector3(0.16f, 0.12f, 0.12f), false);
            var nestLamp = new GameObject("Nest Light").AddComponent<Light>();
            nestLamp.transform.SetParent(nest, false);
            nestLamp.transform.localPosition = new Vector3(-0.3f, 0.5f, 0f);
            nestLamp.type = LightType.Point;
            nestLamp.color = new Color(1f, 0.7f, 0.4f);
            nestLamp.range = 2.5f;
            nestLamp.intensity = 1.2f;
            landmarks.Add(MakeLandmark("Nest Place", nest, nest.position, "NEST", "Spark's nest",
                "your nest: an alcove in the vault wall with the things you kept: a rubber duck, a chipped teacup, a music box that plays four notes", new Vector3(0.8f, 0.6f, 1.4f)));

            var warmth = new GameObject("Vault Light").AddComponent<Light>();
            warmth.transform.SetParent(root, false);
            warmth.transform.localPosition = new Vector3(0f, 2.5f, 2.5f);
            warmth.type = LightType.Point;
            warmth.color = new Color(1f, 0.7f, 0.4f);
            warmth.range = 22f;
            warmth.intensity = 0f;
            warmth.shadows = LightShadows.Soft;
            parts.Light = warmth;
            landmarks.Add(MakeLandmark("Vault Place", root, root.position + Vector3.up * 2f, "VAULT", "the vault door",
                "the vault's steel blast door in its concrete portal in the hill; the valley's last people sleep inside, and the cold is getting in", new Vector3(13f, 6f, 2f)));
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            Bake(probe);
            return parts;
        }

        /// <summary>
        /// The hall's reflections, drawn now as it looks from inside: the cold ambient that
        /// <see cref="Indoors"/> gives it, and no sun.
        /// </summary>
        static void Bake(ReflectionProbe probe)
        {
            var mode = RenderSettings.ambientMode;
            var (sky, equator, ground) = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.045f, 0.055f, 0.075f);
            RenderSettings.ambientEquatorColor = new Color(0.03f, 0.035f, 0.045f);
            RenderSettings.ambientGroundColor = new Color(0.015f, 0.016f, 0.02f);
            var suns = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.enabled).ToList();
            foreach (var s in suns) s.enabled = false;
            string path = $"{Root}/Textures/Hall Reflections.exr";
            bool baked = UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
            foreach (var s in suns) s.enabled = true;
            RenderSettings.ambientMode = mode;
            (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor) = (sky, equator, ground);
            if (!baked)
            {
                Debug.LogWarning("The hall's reflections could not be baked; it reflects the sky.");
                return;
            }
            UnityEditor.AssetDatabase.ImportAsset(path);
            probe.customBakedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        /// <summary>Yellow and black warning stripes, worn, two to every half metre on a block.</summary>
        static Material Hazard()
        {
            var stripes = MakeTexture("Hazard Stripes", 256, false, (u, v) =>
            {
                bool yellow = Mathf.Repeat((u + v) * 2f, 1f) < 0.5f;
                float wear = 0.75f + 0.25f * Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 1f);
                return (yellow ? new Color(0.85f, 0.66f, 0.12f) : new Color(0.07f, 0.07f, 0.07f)) * wear;
            });
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(stripes));
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return MakeMaterial("Hazard Stripes", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", stripes);
                m.SetTextureScale("_BaseMap", Vector2.one / 0.5f);
                m.SetFloat("_Smoothness", 0.3f);
            });
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Frost on a pod's glass: grown in from the edges over most of it, thickest at the bottom, feathered
        /// by noise; the sleeper is a shadow behind it, clearest at the face.
        /// </summary>
        static Color Rime(float u, float v)
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 1.6f, Mathf.Min(v * 0.55f, 1f - v));
            float n = 0f, amplitude = 0.5f, frequency = 5f;
            for (int i = 0; i < 5; i++)
            {
                n += amplitude * Mathf.PerlinNoise(u * frequency + 13.7f * i, v * frequency * 2.6f + 7.1f * i);
                amplitude *= 0.5f;
                frequency *= 2.2f;
            }
            float face = new Vector2((u - 0.5f) * 2.2f, (v - 0.82f) * 3.4f).magnitude;
            float frost = Mathf.Clamp01((0.55f - edge * 1.4f + (n - 0.5f) * 0.7f) * 3.5f) * Mathf.Clamp01(face * 1.6f - 0.2f);
            return new Color(0.84f, 0.92f, 1f, Mathf.Lerp(0.18f, 0.92f, frost));
        }

        /// <summary>A small light that glows (a pod's status), from a plain URP material.</summary>
        static Material Glow(string name, Color colour, float strength) =>
            MakeMaterial(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", colour * 0.4f);
                m.SetFloat("_Smoothness", 0.6f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", colour * strength);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
    }
}
