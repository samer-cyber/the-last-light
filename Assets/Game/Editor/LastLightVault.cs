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
        static readonly int[] Amber = { 9, 26, 35 }, Red = { }, Dark = { 4, 30 }, Empty = { 3, 17, 22, 23, 33 };

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
            var rimeLook = MakeLit("Pod Rime", new Color(0.62f, 0.7f, 0.78f, 0.55f), 0.5f, 0f);
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
                var painted = Load<Texture2D>($"{Root}/Textures/Signs/{sign}.png");
                look.SetTexture("_BaseMap", painted);
                Transparent(look);
                var plate = Box(name, under, look, at, new Vector3(size.x, size.y, 0.004f), false);
                plate.transform.localRotation = turn;
                plate.shadowCastingMode = ShadowCastingMode.Off;
                plate.gameObject.AddComponent<Readable>().Set(painted, "a sign: " + name.ToLowerInvariant(), sign switch
                {
                    "Shelter 7" => "SHELTER 7. SUNVALE CIVIL SHELTER. SEALED YEAR 0",
                    "Pods West" => "PODS 1 - 20",
                    "Pods East" => "PODS 21 - 40",
                    "Keep Clear" => "KEEP DOORS CLEAR. IN COLD SLEEP DO NOT OPEN PODS BY HAND",
                    "Hall A" => "HALL A",
                    "Lumen Mark" => "(the Lumen company's sun mark, no words)",
                    _ => null,
                });
                return plate;
            }
            Renderer Clue(string name, Transform under, string file, Vector3 at, Vector2 size, Quaternion turn, float glow = 0f, bool clear = false)
            {
                var look = MakeLit("Clue " + file, Color.white, 0.2f, 0f);
                var picture = Load<Texture2D>($"{Root}/Textures/Clues/{file}.png");
                look.SetTexture("_BaseMap", picture);
                if (clear) Transparent(look);
                if (glow > 0f)
                {
                    look.EnableKeyword("_EMISSION");
                    look.SetTexture("_EmissionMap", picture);
                    look.SetColor("_EmissionColor", Color.white * glow);
                    look.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                var plate = Box(name, under, look, at, new Vector3(size.x, size.y, 0.004f), false);
                plate.transform.localRotation = turn;
                plate.shadowCastingMode = ShadowCastingMode.Off;
                plate.gameObject.AddComponent<Readable>().Set(picture, name switch
                {
                    "Tomas Scratch" => "words scratched inside pod 3",
                    "Mara Note" => "Mara's note",
                    "Ima Drawing" => "Ima's drawing",
                    "Brothers Scratch" => "words scratched inside pod 22",
                    "Hale Label" => "Dr. Hale's label",
                    "Pod 40 Label" => "your pod's label",
                    "Protocols Poster" => "the makers' protocols",
                    "Status Screen" => "the hall's status screen",
                    "Tally" => "Spark's tally",
                    _ => name.ToLowerInvariant(),
                }, name switch
                {
                    "Tomas Scratch" => "GONE FOR HELP. BACK IN 3 DAYS. - T. VARGA, YR 61",
                    "Mara Note" => "Spark - I'm going for the sun mirror. A Harvester took its heart to the old station past the chasm. I'll bring it back. If I'm not back in ten days, don't follow me. Don't let anyone follow me. Keep the lights on. - Mara",
                    "Ima Drawing" => "(a child's crayon drawing, no words: a little round white robot with one orange eye holding hands with a girl with two pigtails, under a big smiling sun)",
                    "Brothers Scratch" => "BEN + TYL. WE'LL BRING HELP",
                    "Hale Label" => "DR. ADA HALE, YEAR 187. SHE TAUGHT ME THE STARS",
                    "Pod 40 Label" => "40   R. ARCH (frosted over)",
                    "Protocols Poster" => "SUNVALE CIVIL SHELTER 7. CARETAKER PROTOCOLS. 1 KEEP THE SLEEPERS ALIVE. 2 KEEP THE VAULT SEALED: THE DOOR STAYS SHUT UNTIL THE THAW. 3 MAKE NOTHING THAT CAN HARM A PERSON. 4 NEVER LEAVE THE SLEEPERS. 5 WAKE A SLEEPER ONLY IF THEIR POD IS FAILING. Caretaker units 1 to 7 obey these protocols without exception. Report faults to Hall A. ISSUED YEAR 0",
                    "Status Screen" => "SHELTER 7 HALL A STATUS. YEAR 211 OF THE COLD, DAY 47. POWER CELLS 4%. PODS IN HALL 40. EXTERIOR -58 C. VAULT DOOR SEALED/ICE. THAW NOT DETECTED. SUN MIRROR NO SIGNAL",
                    "Tally" => "(rows of chalk marks in fives) 77,112 days. still here.",
                    _ => null,
                });
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
                "the steel blast door out of the vault, from inside. It froze into its frame behind the Lindqvist brothers 71 years ago, and protocol 2 keeps it shut anyway. Heat would melt the ice (a fire beside it, a torch held to it); a hard blow would break it",
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
            // Indoors from the far wall to the blast door itself: the passage through the hill is inside too.
            region.center = new Vector3(0f, HallHeight * 0.5f, (-HallLength + 4.6f) * 0.5f);
            region.size = new Vector3(HallWidth, HallHeight, HallLength + 4.6f);
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
            // The glass itself frosted from the inside, all the way round its curve (Game/Frost).
            var chamberGlass = MakeMaterial("Pod Frost Glass", "Game/Frost", m =>
            {
                m.SetColor("_Color", new Color(0.74f, 0.83f, 0.92f, 1f));
                m.SetFloat("_Density", 0.8f);
                m.SetFloat("_Floor", 0f);
                m.SetFloat("_Face", 1.62f);
                m.SetFloat("_Glow", 0.14f);
            });
            Material GlowOf(Material state) => Art.Variant(chamberGlow, "Chamber Glow " + state.name.Replace("Pod Light ", ""), v =>
                v.SetColor("emissiveFactor", state.GetColor("_EmissionColor")));
            var sleepPose = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(chamberModel)).OfType<AnimationClip>().FirstOrDefault();
            PodLights failing = null;
            Bounds bodyBox = default, glassBox = default;
            // Every pod's own little panel on the pier beside it: a steel box, a screen with its sleeper's state, a lamp.
            var screens = new Dictionary<string, Material>();
            foreach (var state in new[] { "Stable", "Failing", "Critical", "Open", "Dark", "Awake" })
            {
                var picture = Load<Texture2D>($"{Root}/Textures/Clues/Pod Screen {state}.png");
                screens[state] = MakeMaterial("Pod Screen " + state, "Universal Render Pipeline/Unlit", m =>
                {
                    m.SetTexture("_BaseMap", picture);
                    m.SetColor("_BaseColor", Color.white * (state == "Dark" ? 1f : 1.4f));
                });
            }
            Renderer PodPanel(Transform pod, string state, Material lampLook, Vector3 at)
            {
                var housing = Art.Slab("Pod Panel", pod, steel, at, new Vector3(0.24f, 0.32f, 0.08f));
                var face = Box("Pod Panel Screen", housing.transform, screens[state], new Vector3(0f, -0.03f, 0.041f), new Vector3(0.2f, 0.125f, 0.004f), false);
                face.shadowCastingMode = ShadowCastingMode.Off;
                var lamp = Art.Slab("Pod Panel Lamp", housing.transform, lampLook, new Vector3(0.075f, 0.11f, 0.044f), new Vector3(0.03f, 0.03f, 0.012f), false);
                lamp.shadowCastingMode = ShadowCastingMode.Off;
                return housing;
            }
            Transform Pod(int number, float side, float z)
            {
                var pod = new GameObject($"Pod {number}").transform;
                pod.SetParent(hall, false);
                pod.localPosition = new Vector3(side * (half - 0.43f), 0f, z);
                pod.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                bool empty = Empty.Contains(number);
                var status = empty ? dead : Amber.Contains(number) ? amber : Red.Contains(number) ? red : Dark.Contains(number) ? dead : green;
                var chamber = Art.Place(chamberModel, pod, new Vector3(0f, 0f, -0.02f), 2.55f, Quaternion.identity);
                if (sleepPose) sleepPose.SampleAnimation(chamber, 0.45f);
                foreach (var a in chamber.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
                foreach (var a in chamber.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                // The model's own figure goes. One of the shelter's people stands in its place, in the shelter's suit, or
                // nobody, the glass open: five were woken and left.
                foreach (var r in chamber.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterials.Any(m => m && m.name.StartsWith("HumanBody"))) r.enabled = false;
                    else if (r.sharedMaterials.Any(m => m && m.name == "M_glass"))
                    {
                        // Where a person stands in it: on its base, inside the glass, a little back from its middle.
                        if (glassBox.size == Vector3.zero)
                        {
                            glassBox = Art.Measure(r.gameObject, pod);
                            bodyBox = new Bounds(new Vector3(glassBox.center.x, glassBox.min.y + 0.9f, glassBox.center.z - 0.04f), new Vector3(0.5f, 1.8f, 0.36f));
                        }
                        if (empty) r.enabled = false;
                    }
                }
                if (!empty)
                {
                    PutSleeper(pod, bodyBox, number);
                }
                var glow = GlowOf(status);
                foreach (var r in chamber.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Select(m => m == null ? m : m.name == "M_glow" ? glow : m.name == "M_glass" ? chamberGlass : m).ToArray();
                // A faint light inside, the colour of the pod's state; the dead ones are dark.
                Light inside = null;
                if (empty)
                {
                    // The empty ones keep their maintenance light, dim and cold: enough to read by.
                    var service = new GameObject("Pod Service Light").AddComponent<Light>();
                    service.transform.SetParent(pod, false);
                    service.transform.localPosition = new Vector3(0f, 2.1f, 0.25f);
                    service.type = LightType.Point;
                    service.color = new Color(0.7f, 0.82f, 0.95f);
                    service.range = 1.9f;
                    service.intensity = 0.7f;
                    service.shadows = LightShadows.None;
                }
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
                string shows = empty ? "Open" : status == amber ? "Failing" : status == red ? "Critical" : status == dead ? "Dark" : "Stable";
                PodPanel(pod, shows, status, new Vector3(PodPitch * 0.5f - 0.05f, 1.25f, 0.46f));
                // Mr. Okafor's pod fails while the survivor watches (the director calls it).
                if (number == 9)
                {
                    failing = pod.gameObject.AddComponent<PodLights>();
                    var voice = MakeSource("Pod Voice", pod, null, false);
                    voice.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    voice.spatialBlend = 0.85f;
                    voice.minDistance = 2f;
                    voice.maxDistance = 25f;
                    Wire(failing, ("from", glow), ("to", GlowOf(red)), ("inside", inside), ("voice", voice), ("failClip", FindClip("pod_fail")),
                        ("screenFrom", screens["Failing"]), ("screenTo", screens["Critical"]), ("lampFrom", amber), ("lampTo", red));
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
                if (i > 0 && i < alcoves - 1) Pod(20 + i, 1f, z);
                var clueTurn = Quaternion.identity;
                Vector3 inside(float y) => new Vector3(0f, y, glassBox.min.z + 0.12f);
                switch (i + 1)
                {
                    case 3:
                        Clue("Tomas Scratch", west, "Tomas Scratch", inside(1.45f), new Vector2(0.68f, 0.34f), clueTurn, clear: true);
                        landmarks.Add(MakeLandmark("Pod 3 Place", west, west.position + Vector3.up * 1.3f, "POD_3", "pod 3, empty",
                            "pod 3: empty, its glass open. Tomas Varga's: you woke him in year 61; he forced the door with a pry bar to go for help and never came back. He scratched words inside it",
                            new Vector3(1.2f, 2.6f, 1.2f)));
                        break;
                    case 17:
                        Clue("Mara Note", west, "Mara Note", inside(1.42f), new Vector2(0.42f, 0.525f), clueTurn);
                        landmarks.Add(MakeLandmark("Pod 17 Place", west, west.position + Vector3.up * 1.3f, "POD_17", "pod 17, empty",
                            "pod 17: empty. Mara Okonjo's, the engineer you woke in year 98; she stayed a month, then went for the sun mirror and never came back. Her note is still taped inside",
                            new Vector3(1.2f, 2.6f, 1.2f)));
                        break;
                    case 11:
                        Clue("Ima Drawing", west, "Ima Drawing", new Vector3(0.14f, 1.12f, glassBox.max.z + 0.015f), new Vector2(0.42f, 0.42f), clueTurn);
                        landmarks.Add(MakeLandmark("Drawing Place", west, west.TransformPoint(new Vector3(0.14f, 1.12f, glassBox.max.z + 0.02f)), "IMA_DRAWING", "Ima's drawing",
                            "a crayon drawing taped to pod 11's glass, at a child's height: you and Ima holding hands under a smiling sun; she wrote SPARK + IMA and KEEP THE LITES ON!! The night before she went to sleep",
                            new Vector3(0.4f, 0.4f, 0.2f)));
                        break;
                }
            }
            // The empty ones on the east: the brothers' pods, and Dr. Hale's.
            Transform EastPod(int number) => hall.Find($"Pod {number}");
            if (EastPod(22) is Transform brothers)
            {
                Clue("Brothers Scratch", brothers, "Brothers Scratch", new Vector3(0f, 1.5f, glassBox.min.z + 0.12f), new Vector2(0.68f, 0.34f), Quaternion.identity, clear: true);
                landmarks.Add(MakeLandmark("Pod 22 Place", brothers, brothers.position + Vector3.up * 1.3f, "POD_22", "pods 22 and 23, empty",
                    "pods 22 and 23: empty. The Lindqvist brothers, Ben and Tyl, woken the same night in year 140; they left after a week, and the vault door froze behind them. They scratched words inside",
                    new Vector3(1.2f, 2.6f, 1.2f)));
            }
            if (EastPod(33) is Transform hale)
            {
                Clue("Hale Label", hale, "Hale Label", new Vector3(0f, 1.55f, glassBox.min.z + 0.12f), new Vector2(0.62f, 0.18f), Quaternion.identity, clear: true);
                landmarks.Add(MakeLandmark("Pod 33 Place", hale, hale.position + Vector3.up * 1.3f, "POD_33", "pod 33, empty",
                    "pod 33: empty. Dr. Ada Hale's; you woke her in year 187, very weak. She taught you the names of the stars and died in the hall eleven days later, holding your shell. You put a label inside",
                    new Vector3(1.2f, 2.6f, 1.2f)));
            }
            parts.Pod9 = failing;

            // The survivor's pod: pod 40, the last alcove on the east, the same pod as everyone's. Its glass is a lid that
            // Spark cuts free; its label frosted over; its own panel on the pier beside it.
            var mine = new GameObject("Your Pod").transform;
            mine.SetParent(hall, false);
            mine.localPosition = new Vector3(half - 0.43f, 0f, firstPier - (alcoves - 0.5f) * PodPitch);
            mine.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var myChamber = Art.Place(chamberModel, mine, new Vector3(0f, 0f, -0.02f), 2.55f, Quaternion.identity);
            if (sleepPose) sleepPose.SampleAnimation(myChamber, 0.45f);
            foreach (var a in myChamber.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            foreach (var a in myChamber.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            var myGlow = GlowOf(amber);
            foreach (var r in myChamber.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterials.Any(m => m && (m.name.StartsWith("HumanBody") || m.name == "M_glass"))) r.enabled = false;
                else r.sharedMaterials = r.sharedMaterials.Select(m => m && m.name == "M_glow" ? myGlow : m).ToArray();
            }
            // Its floor, to stand on.
            var footing = new GameObject("Pod Footing").AddComponent<BoxCollider>();
            footing.transform.SetParent(mine, false);
            footing.center = new Vector3(bodyBox.center.x, bodyBox.min.y - 0.06f, bodyBox.center.z);
            footing.size = new Vector3(0.9f, 0.12f, Mathf.Max(0.5f, bodyBox.size.z + 0.2f));
            var lidPivot = new GameObject("Lid").transform;
            lidPivot.SetParent(mine, false);
            float lidWide = Mathf.Max(0.7f, glassBox.size.x), lidHigh = Mathf.Max(1.8f, glassBox.size.y);
            lidPivot.localPosition = new Vector3(glassBox.min.x, glassBox.center.y, glassBox.max.z + 0.02f);
            var lidGlass = Box("Lid Glass", lidPivot, podGlass, new Vector3(lidWide * 0.5f, 0f, 0f), new Vector3(lidWide, lidHigh, 0.04f), false);
            lidGlass.shadowCastingMode = ShadowCastingMode.Off;
            var lidFrost = Box("Lid Rime", lidPivot, rimeLook, new Vector3(lidWide * 0.5f, 0f, 0.025f), new Vector3(lidWide, lidHigh, 0.004f), false);
            lidFrost.shadowCastingMode = ShadowCastingMode.Off;
            parts.Lid = lidPivot;
            // The pod's own lamp, still on inside: a cold white light on whoever stands in it.
            var podLamp = new GameObject("Pod Lamp").AddComponent<Light>();
            podLamp.transform.SetParent(mine, false);
            podLamp.transform.localPosition = new Vector3(0f, glassBox.max.y - 0.15f, bodyBox.center.z + 0.15f);
            podLamp.type = LightType.Point;
            podLamp.color = new Color(0.75f, 0.88f, 1f);
            podLamp.range = 2.6f;
            podLamp.intensity = 1.1f;
            podLamp.shadows = LightShadows.Soft;
            Clue("Pod 40 Label", mine, "Pod 40 Label", new Vector3(0f, glassBox.max.y + 0.1f, glassBox.max.z + 0.03f), new Vector2(0.62f, 0.12f), Quaternion.identity);
            // Its own panel on the pier beside it, toward the door, the same as everyone's. It shrieks when the survivor
            // wakes, its lamp blinking red, until they touch it; then it says they are awake.
            var console = PodPanel(mine, "Critical", red, new Vector3(PodPitch * 0.5f - 0.05f, 1.25f, 0.46f));
            console.name = "Pod Console";
            var panel = console.gameObject.AddComponent<Touchable>();
            SetValue(panel, "title", "your pod's panel");
            SetValue(panel, "verb", "touch the panel");
            parts.Panel = panel;
            var panelAlarm = MakeSource("Panel Alarm", console.transform, FindClip("pod_alarm_soft"), true);
            panelAlarm.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            panelAlarm.spatialBlend = 0.8f;
            panelAlarm.minDistance = 2.5f;
            panelAlarm.maxDistance = 30f;
            panelAlarm.volume = 0.4f;
            parts.Alarm = panelAlarm;
            var alarmLamp = new GameObject("Panel Lamp").AddComponent<Light>();
            alarmLamp.transform.SetParent(console.transform, false);
            alarmLamp.transform.localPosition = new Vector3(0.075f, 0.11f, 0.22f);
            alarmLamp.type = LightType.Point;
            alarmLamp.color = new Color(1f, 0.2f, 0.12f);
            alarmLamp.range = 1.3f;
            alarmLamp.intensity = 0f;
            alarmLamp.shadows = LightShadows.None;
            var screen = console.gameObject.AddComponent<PanelScreen>();
            Wire(screen, ("screen", console.transform.Find("Pod Panel Screen").GetComponent<Renderer>()),
                ("lamp", console.transform.Find("Pod Panel Lamp").GetComponent<Renderer>()), ("lampLight", alarmLamp), ("quiet", screens["Awake"]));
            landmarks.Add(MakeLandmark("Your Pod Place", mine, mine.position + Vector3.up * 1.3f, "YOUR_POD", "your pod",
                "pod 40, the pod the survivor woke in, the last on the east side. It cracked in the cold, so you woke them by protocol 5. Its label is frosted over: only the number 40 and the letters R. ARCH can be read; the rest is under the frost",
                new Vector3(1.2f, 2.6f, 1.2f)));
            parts.PodStand = mine.TransformPoint(new Vector3(bodyBox.center.x, bodyBox.min.y, bodyBox.center.z));
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

            Clue("Protocols Poster", hall, "Protocols Poster", new Vector3(-3.3f, 2.05f, -0.035f), new Vector2(0.9f, 1.2f), Quaternion.Euler(0f, 180f, 0f));
            landmarks.Add(MakeLandmark("Poster Place", hall, hall.TransformPoint(new Vector3(-3.3f, 2.05f, -0.1f)), "PROTOCOLS", "the protocols poster",
                "a faded poster on the wall by the door: SUNVALE CIVIL SHELTER 7, CARETAKER PROTOCOLS, your makers' five rules", new Vector3(0.8f, 1f, 0.2f)));
            Clue("Status Screen", hall, "Status Screen", new Vector3(2.05f, 1.55f, -0.035f), new Vector2(0.8f, 0.5f), Quaternion.Euler(0f, 180f, 0f), 1.4f);
            var screenLight = new GameObject("Screen Light").AddComponent<Light>();
            screenLight.transform.SetParent(hall, false);
            screenLight.transform.localPosition = new Vector3(2.05f, 1.55f, -0.45f);
            screenLight.type = LightType.Point;
            screenLight.color = new Color(0.45f, 1f, 0.65f);
            screenLight.range = 2.2f;
            screenLight.intensity = 0.5f;
            landmarks.Add(MakeLandmark("Status Place", hall, hall.TransformPoint(new Vector3(2.05f, 1.55f, -0.1f)), "STATUS", "the hall's status screen",
                "the hall's status screen by the door, still lit: power cells 4%, pods failed this year 2, empty 5, exterior -58 C, thaw not detected, sun mirror no signal",
                new Vector3(0.9f, 0.6f, 0.2f)));

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
            Clue("Tally", nest, "Tally", new Vector3(0.43f, 0.95f, 0f), new Vector2(1.0f, 1.0f), Quaternion.Euler(0f, -90f, 0f), clear: true);
            var shellLook = MakeLit("Old Caretaker Shell", new Color(0.2f, 0.2f, 0.19f), 0.4f, 0.6f);
            var eyeLook = MakeLit("Old Caretaker Eye", new Color(0.02f, 0.02f, 0.025f), 0.85f, 0f);
            for (int k = 0; k < 6; k++)
            {
                var shell = new GameObject($"Caretaker Shell {k + 1}").transform;
                shell.SetParent(nest, false);
                shell.localPosition = new Vector3(-0.05f + (k % 2) * 0.3f, -1.1f, -0.33f + (k / 2) * 0.33f);
                shell.localRotation = Quaternion.Euler(0f, -90f + (k * 37 % 50) - 25f, k * 11 % 20);
                float r = 0.26f;
                var ball = MakePart("Shell", shell, sphere, shellLook, 1f);
                ball.transform.localScale = new Vector3(r, r * 0.92f, r);
                ball.transform.localPosition = new Vector3(0f, r * 0.46f, 0f);
                var eye = MakePart("Dead Eye", shell, sphere, eyeLook, 1f);
                eye.transform.localScale = new Vector3(r * 0.62f, r * 0.42f, r * 0.16f);
                eye.transform.localPosition = new Vector3(0f, r * 0.52f, r * 0.44f);
            }
            var glassesLook = MakeLit("Hale Glasses", new Color(0.18f, 0.14f, 0.1f), 0.6f, 0.5f);
            foreach (float lens in new[] { -0.035f, 0.035f })
            {
                var rim = MakePart("Glasses Rim", nest, cylinder, glassesLook, 1f);
                rim.transform.localPosition = new Vector3(-0.15f, 0.012f, 0.22f + lens);
                rim.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
            }
            var arm = MakePart("Glasses Arm", nest, cube, glassesLook, 1f);
            arm.transform.localPosition = new Vector3(-0.1f, 0.012f, 0.22f);
            arm.transform.localScale = new Vector3(0.11f, 0.004f, 0.004f);
            var nestLamp = new GameObject("Nest Light").AddComponent<Light>();
            nestLamp.transform.SetParent(nest, false);
            nestLamp.transform.localPosition = new Vector3(-0.3f, 0.5f, 0f);
            nestLamp.type = LightType.Point;
            nestLamp.color = new Color(1f, 0.7f, 0.4f);
            nestLamp.range = 2.5f;
            nestLamp.intensity = 0.8f;
            landmarks.Add(MakeLandmark("Nest Place", nest, nest.position, "NEST", "Spark's nest",
                "your nest by the door: an alcove with the things you kept: Ima's rubber duck, a chipped teacup, a music box that plays four notes, Dr. Hale's glasses, and below them the shells of the other six caretakers. On the wall, your tally of days in chalk: 77,112. days. still here.", new Vector3(0.8f, 0.6f, 1.4f)));

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
