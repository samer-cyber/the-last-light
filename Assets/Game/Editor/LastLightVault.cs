using System.Collections.Generic;
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

        const float HallLength = 26f, HallWidth = 9f, HallHeight = 4.2f;

        /// <summary>
        /// The vault, dug into its hill: a round door in a concrete face, and behind it a long cold
        /// hall of sleeping pods, standing in rows along both walls, frost on their glass. Pods 11 and 12
        /// are Ima's and her mother's. The survivor's pod is at the far end, its glass cracked open, its
        /// label frosted. Spark's nest is a hatch in the wall: a duck, a teacup, a music box.
        /// </summary>
        static VaultParts MakeVault(Transform parent, Mesh cube, Mesh cylinder, Mesh sphere, Material concrete, Material metal, Material window,
            Material glass, Material frost, Material light, List<Landmark> landmarks)
        {
            var parts = new VaultParts();
            var root = new GameObject("Vault").transform;
            root.SetParent(parent, false);
            float y = Height(0f, VaultDoorZ + 8f);
            root.position = new Vector3(0f, y, VaultDoorZ);
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
            Box("Face Left", root, concrete, new Vector3(-(door * 0.5f + 2.5f), 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Box("Face Right", root, concrete, new Vector3(door * 0.5f + 2.5f, 3f, -2.5f), new Vector3(5f, 7f, 5f));
            Box("Face Top", root, concrete, new Vector3(0f, doorHigh + 1.8f, -2.5f), new Vector3(door + 0.4f, 3.6f, 5f));
            Box("Lintel", root, metal, new Vector3(0f, doorHigh + 0.25f, 0.1f), new Vector3(13.6f, 0.5f, 0.6f));
            foreach (float side in new[] { -1f, 1f })
                Box("Window", root, window, new Vector3(side * 4.3f, 2.9f, 0.02f), new Vector3(1.1f, 0.5f, 0.05f), false).shadowCastingMode = ShadowCastingMode.Off;
            // The round door turns on a hinge at its left edge.
            var hinge = new GameObject("Door Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(-1.75f, 1.75f, 0.3f);
            var round = MakePart("Door", hinge, cylinder, metal, 1f);
            round.transform.localPosition = new Vector3(1.75f, 0f, 0f);
            round.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            round.transform.localScale = new Vector3(3.7f, 0.2f, 3.7f);
            round.shadowCastingMode = ShadowCastingMode.On;
            round.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.75f, 2f, 0.75f);
            var wheel = MakePart("Wheel", round.transform, cylinder, metal, 1f);
            wheel.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            wheel.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            parts.Door = hinge;

            // The hall behind it, inside the hill.
            var hall = new GameObject("Hall").transform;
            hall.SetParent(root, false);
            hall.localPosition = new Vector3(0f, 0f, -5f);
            float half = HallWidth * 0.5f;
            Box("Floor", hall, concrete, new Vector3(0f, -0.25f, -HallLength * 0.5f), new Vector3(HallWidth + 1f, 0.5f, HallLength + 1f));
            Box("Ceiling", hall, concrete, new Vector3(0f, HallHeight + 0.25f, -HallLength * 0.5f), new Vector3(HallWidth + 1f, 0.5f, HallLength + 1f));
            Box("Wall West", hall, concrete, new Vector3(-half - 0.25f, HallHeight * 0.5f, -HallLength * 0.5f), new Vector3(0.5f, HallHeight, HallLength + 1f));
            Box("Wall East", hall, concrete, new Vector3(half + 0.25f, HallHeight * 0.5f, -HallLength * 0.5f), new Vector3(0.5f, HallHeight, HallLength + 1f));
            Box("Wall End", hall, concrete, new Vector3(0f, HallHeight * 0.5f, -HallLength - 0.25f), new Vector3(HallWidth + 1f, HallHeight, 0.5f));
            // Ribs along the ceiling, and a strip of cold light down the middle, mostly dead.
            for (float z = -2f; z > -HallLength; z -= 3f)
                Box("Rib", hall, metal, new Vector3(0f, HallHeight - 0.15f, z), new Vector3(HallWidth, 0.3f, 0.35f), false);
            var region = new GameObject("Hall Region").AddComponent<BoxCollider>();
            region.transform.SetParent(hall, false);
            region.isTrigger = true;
            region.center = new Vector3(0f, HallHeight * 0.5f, -HallLength * 0.5f);
            region.size = new Vector3(HallWidth, HallHeight, HallLength);
            region.gameObject.layer = 2;
            parts.Hall = region;

            // Pods in rows: a base, a tall glass front with frost on it, a dark shape asleep inside, a small light.
            int number = 1;
            for (int row = 0; row < 7; row++)
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = -3.5f - row * 3f;
                    var pod = new GameObject($"Pod {number}").transform;
                    pod.SetParent(hall, false);
                    pod.localPosition = new Vector3(side * (half - 0.75f), 0f, z);
                    pod.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                    Box("Pod Back", pod, metal, new Vector3(0f, 1.2f, -0.38f), new Vector3(1.1f, 2.4f, 0.14f));
                    Box("Pod Top", pod, metal, new Vector3(0f, 2.45f, -0.12f), new Vector3(1.15f, 0.22f, 0.62f));
                    Box("Pod Base", pod, metal, new Vector3(0f, 0.1f, -0.12f), new Vector3(1.15f, 0.2f, 0.62f));
                    foreach (float rail in new[] { -1f, 1f })
                        Box("Pod Rail", pod, metal, new Vector3(rail * 0.52f, 1.2f, -0.12f), new Vector3(0.1f, 2.4f, 0.6f));
                    var sleeper = MakePart("Sleeper", pod, sphere, metal, 1f);
                    sleeper.transform.localPosition = new Vector3(0f, 1.2f, -0.18f);
                    sleeper.transform.localScale = new Vector3(0.48f, 1.75f, 0.32f);
                    var front = Box("Pod Glass", pod, glass, new Vector3(0f, 1.25f, 0.15f), new Vector3(0.92f, 2.1f, 0.04f));
                    front.shadowCastingMode = ShadowCastingMode.Off;
                    var rime = Box("Pod Frost", pod, frost, new Vector3(0f, 0.55f, 0.18f), new Vector3(0.9f, 0.7f, 0.03f), false);
                    rime.shadowCastingMode = ShadowCastingMode.Off;
                    var tell = MakePart("Pod Light", pod, cube, number == 11 || number == 12 ? light : window, 1f);
                    tell.transform.localPosition = new Vector3(0.38f, 2.35f, 0.38f);
                    tell.transform.localScale = new Vector3(0.12f, 0.06f, 0.04f);
                    if (number == 11 || number == 12)
                        landmarks.Add(MakeLandmark($"Pod {number} Place", pod, pod.position + Vector3.up * 1.2f, $"POD_{number}", $"pod {number}",
                            number == 11 ? "pod 11: Ima sleeps in it, the girl who named Spark; its little light still green" : "pod 12: Ima's mother sleeps in it; its little light still green",
                            new Vector3(1.2f, 2.4f, 1.2f)));
                    number++;
                }
            // Lamps: most dead; a few still burn cold and flicker.
            foreach (float z in new[] { -4f, -13f, -22f })
            {
                var lamp = new GameObject("Hall Lamp").AddComponent<Light>();
                lamp.transform.SetParent(hall, false);
                lamp.transform.localPosition = new Vector3(0f, HallHeight - 0.4f, z);
                lamp.type = LightType.Point;
                lamp.color = new Color(0.65f, 0.8f, 1f);
                lamp.range = 9f;
                lamp.intensity = 1.6f;
                lamp.shadows = LightShadows.Soft;
                lamp.gameObject.AddComponent<Flicker>();
            }
            // The survivor's pod at the far end, cracked open, its label frosted over.
            var mine = new GameObject("Your Pod").transform;
            mine.SetParent(hall, false);
            mine.localPosition = new Vector3(0f, 0f, -HallLength + 1.1f);
            Box("Pod Back", mine, metal, new Vector3(0f, 1.25f, -0.45f), new Vector3(1.25f, 2.5f, 0.14f));
            Box("Pod Top", mine, metal, new Vector3(0f, 2.55f, -0.15f), new Vector3(1.3f, 0.22f, 0.7f));
            foreach (float rail in new[] { -1f, 1f })
                Box("Pod Rail", mine, metal, new Vector3(rail * 0.6f, 1.25f, -0.15f), new Vector3(0.1f, 2.5f, 0.66f));
            var lidPivot = new GameObject("Lid").transform;
            lidPivot.SetParent(mine, false);
            lidPivot.localPosition = new Vector3(-0.55f, 1.25f, 0.3f);
            var lidGlass = Box("Lid Glass", lidPivot, glass, new Vector3(0.55f, 0f, 0f), new Vector3(1.05f, 2.3f, 0.05f), false);
            lidGlass.shadowCastingMode = ShadowCastingMode.Off;
            var lidFrost = Box("Lid Frost", lidPivot, frost, new Vector3(0.55f, -0.75f, 0.03f), new Vector3(1f, 0.8f, 0.03f), false);
            lidFrost.shadowCastingMode = ShadowCastingMode.Off;
            parts.Lid = lidPivot;
            var label = Box("Pod Label", mine, frost, new Vector3(0f, 2.75f, 0.32f), new Vector3(0.9f, 0.18f, 0.03f), false);
            landmarks.Add(MakeLandmark("Your Pod Place", mine, mine.position + Vector3.up * 1.3f, "YOUR_POD", "your pod",
                "the pod the survivor woke in, at the end of the hall; its glass cracked open in the cold. Its label is frosted; scraped clean it reads: ARCHITECT. LUMEN MIND SYSTEMS",
                new Vector3(1.4f, 2.6f, 1.2f)));
            parts.PodStand = mine.position + mine.forward * 0.35f;
            parts.PodFacing = mine.forward;
            // Spark's nest: a hatch in the east wall near the end, with what it kept.
            var nest = new GameObject("Nest").transform;
            nest.SetParent(hall, false);
            nest.localPosition = new Vector3(half - 0.2f, 1.1f, -HallLength + 4.5f);
            Box("Nest Shelf", nest, metal, new Vector3(0f, -0.05f, 0f), new Vector3(0.6f, 0.08f, 1.2f), false);
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
                "your nest: a hatch in the vault wall with the things you kept: a rubber duck, a chipped teacup, a music box that plays four notes", new Vector3(0.8f, 0.6f, 1.4f)));

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
            return parts;
        }
    }
}
