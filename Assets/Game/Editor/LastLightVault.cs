using System.Collections.Generic;
using System.Linq;
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
            Material glass, Material frost, Material light, List<Landmark> landmarks)
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

            // The face in the hill, with a doorway 3.2 m wide and 3.4 m high.
            const float door = 3.2f, doorHigh = 3.4f;
            Art.Slab("Face Left", root, wall, new Vector3(-(door * 0.5f + 2.5f), 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Art.Slab("Face Right", root, wall, new Vector3(door * 0.5f + 2.5f, 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Art.Slab("Face Top", root, wall, new Vector3(0f, doorHigh + 1.8f, -2.5f), new Vector3(door + 0.4f, 3.6f, 5f));
            Art.Slab("Lintel", root, rust, new Vector3(0f, doorHigh + 0.25f, 0.1f), new Vector3(13.6f, 0.5f, 0.6f));
            foreach (float side in new[] { -1f, 1f })
                Box("Window", root, window, new Vector3(side * 4.3f, 2.9f, 0.02f), new Vector3(1.1f, 0.5f, 0.05f), false).shadowCastingMode = ShadowCastingMode.Off;
            // The round door turns on a hinge at its left edge.
            var hinge = new GameObject("Door Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(-1.75f, 1.75f, 0.3f);
            var round = MakePart("Door", hinge, cylinder, worn, 1f);
            round.transform.localPosition = new Vector3(1.75f, 0f, 0f);
            round.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            round.transform.localScale = new Vector3(3.7f, 0.2f, 3.7f);
            round.shadowCastingMode = ShadowCastingMode.On;
            round.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.75f, 2f, 0.75f);
            var wheel = MakePart("Wheel", round.transform, cylinder, steel, 1f);
            wheel.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            wheel.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            parts.Door = hinge;

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
            var burning = new[] { -3f, -12f, -21f };
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

            // The pods, one to an alcove: a steel plinth with a tread plate, a panelled back, painted posts,
            // a housing on top with its little light, the glass with frost on it, a sleeper's dark shape.
            Transform Pod(int number, float side, float z)
            {
                var pod = new GameObject($"Pod {number}").transform;
                pod.SetParent(hall, false);
                pod.localPosition = new Vector3(side * (half - 0.43f), 0f, z);
                pod.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                Art.Slab("Pod Plinth", pod, steel, new Vector3(0f, 0.11f, -0.02f), new Vector3(1.08f, 0.22f, 0.8f), false);
                Art.Slab("Pod Tread", pod, tread, new Vector3(0f, 0.225f, 0.03f), new Vector3(0.98f, 0.012f, 0.66f), false);
                Art.Slab("Pod Back", pod, worn, new Vector3(0f, 1.42f, -0.36f), new Vector3(0.98f, 2.4f, 0.08f), false);
                foreach (float post in new[] { -1f, 1f })
                    Art.Slab("Pod Post", pod, steel, new Vector3(post * 0.48f, 1.42f, -0.02f), new Vector3(0.12f, 2.4f, 0.78f), false);
                Art.Slab("Pod Housing", pod, steel, new Vector3(0f, 2.78f, -0.02f), new Vector3(1.08f, 0.34f, 0.8f), false);
                Art.Slab("Pod Panel", pod, steel, new Vector3(0f, 2.78f, 0.385f), new Vector3(0.72f, 0.18f, 0.02f), false);
                var status = Amber.Contains(number) ? amber : Red.Contains(number) ? red : Dark.Contains(number) ? dead : green;
                var tell = MakePart("Pod Light", pod, cube, status, 1f);
                tell.transform.localPosition = new Vector3(0.24f, 2.78f, 0.4f);
                tell.transform.localScale = new Vector3(0.14f, 0.05f, 0.015f);
                // The sleeper, a shape behind the frost: head, shoulders and body, legs.
                foreach (var (at, size) in new[] { (new Vector3(0f, 1.98f, -0.17f), new Vector3(0.19f, 0.23f, 0.2f)), (new Vector3(0f, 1.52f, -0.19f), new Vector3(0.38f, 0.62f, 0.2f)),
                             (new Vector3(0f, 0.8f, -0.19f), new Vector3(0.27f, 0.9f, 0.17f)) })
                {
                    var shape = MakePart("Sleeper", pod, sphere, sleeperLook, 1f);
                    shape.transform.localPosition = at;
                    shape.transform.localScale = size;
                }
                // A faint light inside, the colour of the pod's state; the dead ones are dark.
                if (status != dead)
                {
                    var inside = new GameObject("Pod Inside").AddComponent<Light>();
                    inside.transform.SetParent(pod, false);
                    inside.transform.localPosition = new Vector3(0f, 2.4f, 0.02f);
                    inside.type = LightType.Point;
                    inside.color = status == green ? new Color(0.55f, 0.92f, 0.95f) : status == amber ? new Color(1f, 0.62f, 0.3f) : new Color(1f, 0.25f, 0.2f);
                    inside.range = 1.7f;
                    inside.intensity = 0.9f;
                    inside.shadows = LightShadows.None;
                }
                var front = Box("Pod Glass", pod, podGlass, new Vector3(0f, 1.42f, 0.3f), new Vector3(0.84f, 2.3f, 0.03f), false);
                front.shadowCastingMode = ShadowCastingMode.Off;
                var rime = Box("Pod Rime", pod, rimeLook, new Vector3(0f, 1.42f, 0.318f), new Vector3(0.84f, 2.3f, 0.004f), false);
                rime.shadowCastingMode = ShadowCastingMode.Off;
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
                if (i > 0) Pod(20 + i, 1f, z);
            }

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
            Art.Slab("Pod Label", mine, worn, new Vector3(0f, 2.92f, 0.4f), new Vector3(0.9f, 0.16f, 0.02f), false);
            // Its own panel: a steel console beside it, the instrument set into its top.
            Art.Slab("Pod Console", mine, steel, new Vector3(1.08f, 0.55f, 0.12f), new Vector3(0.42f, 1.1f, 0.32f));
            Art.Prop("vintage_spacecraft_instrument", mine, new Vector3(1.08f, 1.1f, 0.14f), Quaternion.Euler(-25f, -15f, 0f));
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
                "the round door of the vault in the hill; the valley's last people sleep inside, and the cold is getting in", new Vector3(13f, 6f, 2f)));
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
