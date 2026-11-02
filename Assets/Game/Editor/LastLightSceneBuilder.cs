using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static Game.Building.SceneKit;
using Object = UnityEngine.Object;

namespace Game.Building
{
    /// <summary>
    /// Builds the end of time from nothing: the frozen ground with its chasm and its mountains, the
    /// dying sun and the black hole, the traveller (a jointed figure in a cloak), the camera at their
    /// shoulder, the mind, the loose stone, ice and metal, the ruins, the first ember, the maker, the
    /// reader, the voice and the director, all wired together. Running it again replaces the scene.
    /// </summary>
    public static class LastLightSceneBuilder
    {
        const string ScenePath = Root + "/Scenes/LastLight.unity";
        static readonly Vector3 SunDirection = new Vector3(-0.38f, 0.16f, 0.91f).normalized;
        static readonly Vector3 HoleDirection = new Vector3(0.55f, 0.52f, 0.65f).normalized;
        const float ChasmNear = 0f, ChasmFar = 12f;
        static readonly Vector3 Start = new Vector3(0f, 0f, -14f);

        [MenuItem("Game/Build Last Light Scene")]
        public static void Build()
        {
            foreach (string folder in new[] { "Scenes", "Materials", "Textures", "Meshes", "Settings", "UI", "Story" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{folder}")) AssetDatabase.CreateFolder(Root, folder);

            var softDot = MakeTexture("Soft Dot", 64, true, SoftDot);
            var softLine = MakeTexture("Soft Line", 32, true, (u, v) => new Color(1f, 1f, 1f, Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.18f, 2f))));
            var sphere = MakeSphere(160, 96);
            var billboard = MakeBillboard();
            var ground = MakeGround();
            var disc = MakeRing("Accretion Ring", 128, 0.32f, 1f);
            var rocks = Enumerable.Range(0, 4).Select(i => MakeRock($"Rock {i}", 11 + i * 7, 0.28f)).ToArray();
            var shard = MakeRock("Shard", 3, 0.45f);
            var monolith = MakeRock("Monolith", 21, 0.12f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            // ---- Materials ----
            var sky = MakeMaterial("Dying Sky", "Game/DyingSky", m => m.SetVector("_SunDirection", SunDirection));
            var groundMaterial = MakeMaterial("Frozen Ground", "Game/Ground");
            var stone = MakeMatter("Matter Stone", new Color(0.5f, 0.48f, 0.47f), new Color(0.33f, 0.32f, 0.33f), 0.18f, 0f, new Color(0.55f, 0.54f, 0.52f), Color.black, Color.black);
            var ice = MakeMatter("Matter Ice", new Color(0.42f, 0.6f, 0.72f), new Color(0.28f, 0.44f, 0.58f), 0.9f, 0f, new Color(0.75f, 0.88f, 0.95f), new Color(0.12f, 0.3f, 0.45f), Color.black);
            var metal = MakeMatter("Matter Metal", new Color(0.3f, 0.29f, 0.3f), new Color(0.16f, 0.15f, 0.17f), 0.62f, 0.85f, new Color(0.62f, 0.58f, 0.52f), Color.black, Color.black);
            var light = MakeMatter("Matter Light", new Color(1f, 0.9f, 0.75f), new Color(1f, 0.8f, 0.6f), 0.5f, 0f, new Color(1f, 0.95f, 0.85f), new Color(1.2f, 0.8f, 0.5f), new Color(2.4f, 1.7f, 1.1f));
            var matters = new[] { stone, ice, metal, light };
            var ghost = MakeMaterial("Plan Ghost", "Game/Ghost", m =>
            {
                m.SetColor("_Color", new Color(1.3f, 0.95f, 0.65f));
                m.SetFloat("_Intensity", 0.42f);
                m.SetFloat("_Edge", 1.1f);
                m.SetFloat("_Rim", 0.45f);
            });
            var ideaGhost = MakeMaterial("Idea Ghost", "Game/Ghost", m =>
            {
                m.SetColor("_Color", new Color(0.6f, 0.8f, 1.3f));
                m.SetFloat("_Intensity", 0.36f);
                m.SetFloat("_Lines", 0.6f);
            });
            var sunMaterial = MakeMaterial("Dying Sun", "Game/Sun", m =>
            {
                m.SetColor("_Deep", new Color(0.5f, 0.06f, 0.02f));
                m.SetColor("_Bright", new Color(1f, 0.32f, 0.12f));
                m.SetFloat("_Spots", 0.85f);
                m.SetFloat("_Intensity", 1.6f);
            });
            var sunGlow = MakeMaterial("Dying Sun Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.25f, 0.08f));
                m.SetFloat("_Edge", 1f / 3.2f);
                m.SetFloat("_Intensity", 0.55f);
            });
            var black = MakeMaterial("Black Hole", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", Color.black));
            var accretion = MakeMaterial("Accretion Disc", "Game/Accretion", m => m.SetFloat("_Intensity", 1.1f));
            var mindCore = MakeMaterial("Mind Core", "Game/OrbCore", m =>
            {
                m.SetColor("_Deep", new Color(0.1f, 0.04f, 0.02f));
                m.SetColor("_Mid", new Color(0.9f, 0.45f, 0.18f));
                m.SetColor("_Hot", new Color(1f, 0.85f, 0.6f));
                m.SetColor("_Core", new Color(1f, 0.97f, 0.9f));
                m.SetFloat("_Intensity", 2.4f);
            });
            var mindCorona = MakeMaterial("Mind Corona", "Game/Corona", m =>
            {
                m.SetColor("_Inner", new Color(1f, 0.92f, 0.78f));
                m.SetColor("_Outer", new Color(1f, 0.55f, 0.25f));
                m.SetFloat("_Edge", 0.1f);
                m.SetFloat("_Intensity", 1.3f);
            });
            var mindGlow = MakeMaterial("Mind Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.72f, 0.45f));
                m.SetFloat("_Edge", 0.12f);
                m.SetFloat("_Intensity", 0.7f);
            });
            var emberCore = MakeMaterial("Ember Core", "Game/OrbCore", m =>
            {
                m.SetColor("_Deep", new Color(0.2f, 0.03f, 0.01f));
                m.SetColor("_Mid", new Color(1f, 0.35f, 0.08f));
                m.SetColor("_Hot", new Color(1f, 0.75f, 0.35f));
                m.SetColor("_Core", new Color(1f, 0.95f, 0.8f));
                m.SetFloat("_Intensity", 3f);
            });
            var emberGlow = MakeMaterial("Ember Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.5f, 0.18f));
                m.SetFloat("_Edge", 0.1f);
                m.SetFloat("_Intensity", 1.1f);
            });
            var snowMaterial = MakeParticleMaterial("Falling Snow", softDot, 0.35f);
            var markMaterial = MakeParticleMaterial("Mind Mark", softLine, 3.5f);
            var porcelain = MakeLit("Traveller Porcelain", new Color(0.78f, 0.76f, 0.72f), 0.62f, 0f);
            var joint = MakeLit("Traveller Joint", new Color(0.09f, 0.09f, 0.1f), 0.55f, 0.75f);
            var cloth = MakeLit("Traveller Cloak", new Color(0.05f, 0.055f, 0.065f), 0.18f, 0f);
            cloth.SetFloat("_Cull", 0f);
            cloth.doubleSidedGI = true;
            var pack = MakeLit("Traveller Pack", new Color(0.13f, 0.12f, 0.11f), 0.35f, 0.3f);
            var slippery = MakePhysics("Slippery", 0.04f);

            var profile = MakeProfile();
            var panel = MakePanel();

            // ---- The scene ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.115f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.08f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.045f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.032f, 0.032f, 0.045f);
            RenderSettings.fogDensity = 0.011f;
            int travellerLayer = EnsureLayer("Traveller");

            // The dying sun: huge, dim and red on the horizon; its light is the scene's main light.
            var sunObject = new GameObject("Dying Sun");
            sunObject.transform.position = SunDirection * 3200f;
            MakePart("Body", sunObject.transform, sphere, sunMaterial, 1100f);
            MakePart("Glow", sunObject.transform, billboard, sunGlow, 1100f * 3.2f);
            var sunlight = new GameObject("Sunlight").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sunlight.color = new Color(1f, 0.42f, 0.24f);
            sunlight.intensity = 1.7f;
            sunlight.shadows = LightShadows.Soft;
            sunlight.shadowStrength = 0.85f;
            RenderSettings.sun = sunlight;

            // The black hole, high in the sky, bending the stars around it.
            var holeObject = new GameObject("Black Hole");
            holeObject.transform.position = HoleDirection * 3000f;
            holeObject.transform.rotation = Quaternion.LookRotation(HoleDirection) * Quaternion.Euler(76f, 0f, 18f);
            MakePart("Horizon", holeObject.transform, sphere, black, 150f);
            var discRenderer = MakePart("Disc", holeObject.transform, disc, accretion, 520f);
            discRenderer.transform.localRotation = Quaternion.identity;

            // The ground.
            var groundObject = new GameObject("Ground");
            groundObject.AddComponent<MeshFilter>().sharedMesh = ground;
            var groundRenderer = groundObject.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            groundObject.AddComponent<MeshCollider>().sharedMesh = ground;
            float startY = Height(Start.x, Start.z);

            // ---- The traveller ----
            var travellerObject = new GameObject("Traveller") { layer = travellerLayer };
            travellerObject.transform.position = new Vector3(Start.x, startY + 0.05f, Start.z);
            var controller = travellerObject.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.93f, 0f);
            controller.height = 1.82f;
            controller.radius = 0.28f;
            controller.slopeLimit = 52f;
            controller.stepOffset = 0.42f;
            controller.skinWidth = 0.03f;
            var figure = MakeFigure(travellerObject.transform, travellerLayer, porcelain, joint, cloth, pack);

            // ---- The camera ----
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetPositionAndRotation(travellerObject.transform.position + new Vector3(0.6f, 2f, -4.4f), Quaternion.identity);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 6000f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderShadows = true;
            cameraData.dithering = true;
            cameraData.stopNaN = true;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FollowCamera>();
            Wire(follow, ("view", camera), ("target", travellerObject.transform));
            SetMask(follow, "solid", ~(1 << travellerLayer) & ~(1 << 2));
            var warp = holeObject.AddComponent<SpaceWarp>();
            Wire(warp, ("view", camera));
            SetValue(warp, "ring", 3.2f);
            SetValue(warp, "swirl", 0.12f);
            SetValue(warp, "near", 400f);

            // Snow, drifting past the camera all the time.
            var snow = MakeParticles("Snow", cameraObject.transform, snowMaterial);
            Snow(snow);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            // ---- Sound ----
            var sound = new GameObject("Sound").transform;
            var ambience = MakeSource("Dying Sun", sound, FindClip("world_sun_loop") ?? FindClip("void_drone_v2"), true);
            var windSource = MakeSource("Wind", sound, FindClip("world_wind_loop"), true);
            var music = MakeSource("Music", sound, FindClip("lastlight_1") ?? FindClip("music_void"), true);
            var stepsSource = MakeSource("Steps", sound, null, false);
            var makerSound = MakeSource("Making", sound, null, false);
            var talkSound = MakeSource("Talk", sound, null, false);
            var typeSound = MakeSource("Typing", sound, null, false);

            // ---- The mind ----
            var mindObject = new GameObject("Mind");
            mindObject.transform.position = travellerObject.transform.position + new Vector3(0.85f, 2.05f, -0.45f);
            var core = MakePart("Core", mindObject.transform, sphere, mindCore, 0.16f);
            var corona = MakePart("Corona", mindObject.transform, billboard, mindCorona, 1.6f);
            var glow = MakePart("Glow", mindObject.transform, billboard, mindGlow, 1.4f);
            var shine = new GameObject("Shine").AddComponent<Light>();
            shine.transform.SetParent(mindObject.transform, false);
            shine.type = LightType.Point;
            shine.color = new Color(1f, 0.82f, 0.62f);
            shine.range = 9f;
            shine.intensity = 2.6f;
            shine.shadows = LightShadows.Soft;
            shine.shadowStrength = 0.7f;
            var markLine = new GameObject("Mark").AddComponent<LineRenderer>();
            markLine.transform.SetParent(mindObject.transform, false);
            markLine.useWorldSpace = false;
            markLine.sharedMaterial = markMaterial;
            markLine.widthMultiplier = 0.06f;
            markLine.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.55f));
            markLine.numCapVertices = 4;
            markLine.numCornerVertices = 2;
            markLine.alignment = LineAlignment.View;
            markLine.shadowCastingMode = ShadowCastingMode.Off;
            var hum = MakeSource("Hum", mindObject.transform, FindClip("companion_hum_loop"), true);
            hum.spatialBlend = 0.6f;
            hum.playOnAwake = true;
            hum.volume = 0.3f;
            var voice = MakeSource("Voice", mindObject.transform, null, false);
            var mind = mindObject.AddComponent<Companion>();
            Wire(mind, ("traveller", travellerObject.transform), ("travellerBody", figure.Body), ("core", core), ("corona", corona), ("glow", glow),
                ("shine", shine), ("mark", markLine), ("hum", hum), ("voice", voice), ("pieceMesh", shard),
                ("yesClip", FindClip("companion_yes")), ("noClip", FindClip("companion_no")), ("cannotClip", FindClip("companion_cannot")),
                ("takeClip", FindClip("foley_pickup_stone_1")), ("emberClip", FindClip("companion_ember")), ("thinkClip", FindClip("companion_think_loop")));
            WireArray(mind, "pieceMaterials", matters);

            // ---- The interface ----
            var hudStyle = Load<StyleSheet>($"{Root}/UI/Hud.uss");
            var sayStyle = Load<StyleSheet>($"{Root}/UI/Say.uss");
            var interfaceObject = new GameObject("Interface");
            var document = interfaceObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var hud = interfaceObject.AddComponent<Hud>();
            Wire(hud, ("document", document), ("style", hudStyle));

            // ---- The traveller's parts, now that everything they talk to exists ----
            var traveller = travellerObject.AddComponent<Traveller>();
            Wire(traveller, ("follow", follow), ("mind", mind), ("hud", hud), ("body", figure.Body));
            SetMask(traveller, "seen", ~(1 << travellerLayer) & ~(1 << 2));
            var body = figure.Body.gameObject.AddComponent<TravellerBody>();
            Wire(body, ("traveller", traveller), ("hips", figure.Hips), ("spine", figure.Spine), ("chest", figure.Chest), ("neck", figure.Neck),
                ("head", figure.Head), ("cloak", figure.Cloak), ("lookAt", mindObject.transform));
            SetSide(body, "left", figure.Left);
            SetSide(body, "right", figure.Right);

            var say = interfaceObject.AddComponent<SayLine>();
            Wire(say, ("document", document), ("style", sayStyle), ("traveller", traveller), ("follow", follow), ("sound", typeSound));
            WireArray(say, "keyClips", Enumerable.Range(1, 6).Select(i => (Object)FindClip($"pencil_stroke_{i}")).Where(c => c != null).ToArray());
            var talk = interfaceObject.AddComponent<PushToTalk>();
            Wire(talk, ("hud", hud), ("sound", talkSound), ("startClip", FindClip("companion_listen") ?? FindClip("listen_on")), ("stopClip", FindClip("listen_off")));

            // ---- The world's things ----
            var landmarks = new List<Landmark>();
            var world = new GameObject("World").transform;
            // The chasm and the far side.
            landmarks.Add(MakeLandmark("Chasm", world, new Vector3(0f, (startY - 1.5f - 46f) * 0.5f, (ChasmNear + ChasmFar) * 0.5f), "CHASM", "the chasm",
                $"{ChasmFar - ChasmNear:F0} m wide and very deep; nothing crosses it", new Vector3(60f, startY - 1.5f + 46f, ChasmFar - ChasmNear - 0.6f)));
            float farY = Height(0f, ChasmFar + 4f);
            var farSide = MakeLandmark("Far Side", world, new Vector3(0f, farY, ChasmFar + 4f), "FAR_SIDE", "the far side", "the ground across the chasm", Vector3.zero);
            var farRegion = farSide.gameObject.AddComponent<BoxCollider>();
            farRegion.isTrigger = true;
            farRegion.center = new Vector3(0f, 0f, 16f);
            farRegion.size = new Vector3(70f, 6f, 38f);
            landmarks.Add(farSide);

            // The first ember, on a plinth across the chasm.
            var plinthPosition = new Vector3(2.5f, Height(2.5f, ChasmFar + 9f), ChasmFar + 9f);
            var plinth = MakePart("Plinth", world, cube, stone, 1f);
            plinth.transform.position = plinthPosition + Vector3.up * 0.55f;
            plinth.transform.localScale = new Vector3(1.1f, 1.3f, 1.1f);
            plinth.shadowCastingMode = ShadowCastingMode.On;
            plinth.receiveShadows = true;
            plinth.gameObject.AddComponent<BoxCollider>();
            var ember = MakeEmber("First Ember", world, plinthPosition + Vector3.up * 2.1f, sphere, billboard, emberCore, emberGlow);
            landmarks.Add(MakeLandmark("Ember Place", ember.transform, ember.transform.position, "EMBER_1", "an ember of light", "one of the last three; it hangs above a stone plinth across the chasm", Vector3.one * 1.2f));

            // The second ember, sealed in a square ruin whose door opens only under weight.
            var (gate, ruinEmber) = MakeRuin(world, new Vector3(17f, 0f, 37f), cube, metal, stone, light, sphere, billboard, emberCore, emberGlow, landmarks, travellerObject.transform);

            // The third ember, on the shelf above the cliff; ice lies at the cliff's foot.
            float topY = Height(-8f, 64f);
            var cliffPlinth = MakePart("Cliff Plinth", world, cube, stone, 1f);
            cliffPlinth.transform.position = new Vector3(-8f, topY + 0.45f, 64f);
            cliffPlinth.transform.localScale = new Vector3(1f, 1.1f, 1f);
            cliffPlinth.shadowCastingMode = ShadowCastingMode.On;
            cliffPlinth.receiveShadows = true;
            cliffPlinth.gameObject.AddComponent<BoxCollider>();
            var cliffEmber = MakeEmber("Cliff Ember", world, new Vector3(-8f, topY + 1.9f, 64f), sphere, billboard, emberCore, emberGlow);
            landmarks.Add(MakeLandmark("Ember Place", cliffEmber.transform, cliffEmber.transform.position, "EMBER_3", "an ember of light", "the last of the three; it rests on the shelf above the cliff", Vector3.one * 1.2f));
            float footY = Height(-8f, 52f);
            landmarks.Add(MakeLandmark("Cliff", world, new Vector3(-8f, footY + 5f, 57.2f), "CLIFF", "the cliff",
                $"a wall of rock about {topY - footY:F0} m high, too steep to climb", new Vector3(70f, 12f, 4.5f)));
            landmarks.Add(MakeLandmark("Cliff Top", world, new Vector3(-8f, topY, 62f), "CLIFF_TOP", "the top of the cliff",
                $"a shelf {topY - footY:F0} m above the ground below the cliff", new Vector3(70f, 4f, 9f)));

            // Loose stone near the start; ice on the frozen lake; metal by the ruins.
            var random = new System.Random(42);
            var pieces = new GameObject("Loose Pieces").transform;
            var stonePlaces = new[] { new Vector2(-4f, -9f), new Vector2(5f, -11f), new Vector2(-7f, -17f), new Vector2(8f, -19f), new Vector2(-2f, -23f), new Vector2(3f, -6f), new Vector2(-11f, -12f), new Vector2(12f, -14f), new Vector2(0f, -30f), new Vector2(-15f, -25f) };
            foreach (var p in stonePlaces) MakePickup(pieces, rocks[random.Next(rocks.Length)], stone, Matter.Stone, 2, p, 0.45f + 0.15f * (float)random.NextDouble(), random);
            for (int i = 0; i < 7; i++)
                MakePickup(pieces, rocks[random.Next(rocks.Length)], ice, Matter.Ice, 2, new Vector2(-36f + 10f * (float)random.NextDouble(), -30f + 12f * (float)random.NextDouble()), 0.5f, random);
            // Ruins: dark slabs of the last builders, half sunk, with metal scattered round them.
            var ruins = new GameObject("Ruins").transform;
            for (int i = 0; i < 4; i++)
            {
                var slab = MakePart($"Slab {i}", ruins, monolith, metal, 1f);
                var at = new Vector2(24f + 5f * i, -18f + 3f * Mathf.Sin(i * 2.1f));
                slab.transform.position = new Vector3(at.x, Height(at.x, at.y) + 1.2f, at.y);
                slab.transform.localScale = new Vector3(1.4f, 4.5f + i, 0.6f);
                slab.transform.rotation = Quaternion.Euler(8f * Mathf.Sin(i * 3f), 20f * i, 12f * Mathf.Cos(i * 2f));
                slab.shadowCastingMode = ShadowCastingMode.On;
                slab.receiveShadows = true;
                slab.gameObject.AddComponent<MeshCollider>().sharedMesh = monolith;
            }
            landmarks.Add(MakeLandmark("Ruins Place", ruins, new Vector3(31f, Height(31f, -18f), -18f), "RUINS", "the ruins", "dark slabs left by the last builders; metal lies round them", new Vector3(18f, 6f, 8f)));
            for (int i = 0; i < 6; i++)
                MakePickup(pieces, shard, metal, Matter.Metal, 2, new Vector2(22f + 16f * (float)random.NextDouble(), -23f + 9f * (float)random.NextDouble()), 0.6f, random);
            landmarks.Add(MakeLandmark("Lake Place", pieces, new Vector3(-31f, Height(-31f, -24f), -24f), "FROZEN_LAKE", "the frozen lake", "old ice; loose ice lies on it", new Vector3(18f, 1f, 14f)));
            foreach (var p in new[] { new Vector2(-14f, 47f), new Vector2(-10f, 44f), new Vector2(-4f, 48f), new Vector2(-17f, 51f), new Vector2(-1f, 52f), new Vector2(-12f, 52.5f), new Vector2(-7f, 41f), new Vector2(3f, 46f) })
                MakePickup(pieces, rocks[random.Next(rocks.Length)], ice, Matter.Ice, 2, p, 0.55f, random);
            foreach (var p in new[] { new Vector2(6f, 27f), new Vector2(-6f, 30f), new Vector2(9f, 44f) })
                MakePickup(pieces, rocks[random.Next(rocks.Length)], stone, Matter.Stone, 2, p, 0.5f, random);
            foreach (var p in new[] { new Vector2(25f, 30f), new Vector2(27f, 42f), new Vector2(10f, 33f) })
                MakePickup(pieces, shard, metal, Matter.Metal, 2, p, 0.6f, random);

            // ---- The maker, the reader and the director ----
            var makerObject = new GameObject("Maker");
            var maker = makerObject.AddComponent<Maker>();
            Wire(maker, ("cube", cube), ("cylinder", cylinder), ("sphere", ball), ("shard", shard), ("ghost", ghost), ("ideaGhost", ideaGhost),
                ("slippery", slippery), ("sound", makerSound), ("doneClip", FindClip("build_complete_1")), ("apartClip", FindClip("build_apart_1")),
                ("planClip", FindClip("build_plan_1")));
            WireArray(maker, "matter", matters);
            WireArray(maker, "flyClips", new[] { "build_fly_1", "build_fly_2", "build_fly_3" }.Select(n => (Object)FindClip(n)).Where(c => c != null).ToArray());
            WireArray(maker, "lockClips", new[] { "build_lock_stone_1", "build_lock_ice_1", "build_lock_metal_1", "build_plan_2" }.Select(n => (Object)FindClip(n)).ToArray());
            SetMask(maker, "ground", ~(1 << travellerLayer) & ~(1 << 2));

            var reader = new GameObject("Reader").AddComponent<Mind>();
            Wire(reader, ("card", Load<TextAsset>($"{Root}/Story/Last Light card.txt")));

            var director = new GameObject("Director").AddComponent<LastLightDirector>();
            Wire(director, ("traveller", traveller), ("follow", follow), ("body", body), ("mind", mind), ("maker", maker), ("reader", reader),
                ("say", say), ("talk", talk), ("hud", hud), ("firstIdeaTo", landmarks.First(l => l.Id == "FAR_SIDE")),
                ("ambience", ambience), ("wind", windSource), ("music", music), ("steps", stepsSource),
                ("jumpClip", FindClip("foley_jump_1")), ("landClip", FindClip("foley_land_1")));
            WireArray(director, "embers", new Object[] { ember, ruinEmber, cliffEmber });
            var mindWarp = mindObject.AddComponent<SpaceWarp>();
            Wire(mindWarp, ("view", camera));
            SetValue(mindWarp, "ring", 4f);
            SetValue(mindWarp, "near", 1.5f);
            Wire(director, ("gate", gate), ("holeWarp", warp), ("mindWarp", mindWarp), ("finaleClip", FindClip("finale_2")));
            WireArray(director, "landmarks", landmarks.Cast<Object>().ToArray());
            WireArray(director, "snowSteps", Clips("foley_step_snow", 6));
            WireArray(director, "stoneSteps", Clips("foley_step_stone", 6));
            WireArray(director, "iceSteps", Clips("foley_step_ice", 6));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int opening = scenes.FindIndex(s => s.path.EndsWith("/Opening.unity"));
            scenes.Insert(opening + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}");
        }

        static Object[] Clips(string stem, int count) =>
            Enumerable.Range(1, count).Select(i => (Object)FindClip($"{stem}_{i}")).Where(c => c != null).ToArray();

        // ---- The ground ----

        /// <summary>
        /// The height of the ground: a snowfield with low drifts, the chasm across it, the far side a
        /// little higher, a cliff further on, a frozen lake in a hollow, mountains all round.
        /// </summary>
        public static float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 7.7f) - 0.5f;
            float fine = Mathf.PerlinNoise(x * 0.17f + 1.3f, z * 0.17f + 9.4f) - 0.5f;
            float h = n * 1.6f + fine * 0.35f;
            // The far side, and a cliff beyond it with a shelf on top.
            float near = ChasmNear + 1.6f * (Mathf.PerlinNoise(x * 0.06f, 0.5f) - 0.5f) * Mathf.Clamp01(Mathf.Abs(x) / 12f);
            float far = ChasmFar + 1.6f * (Mathf.PerlinNoise(x * 0.06f + 5f, 2.5f) - 0.5f) * Mathf.Clamp01(Mathf.Abs(x) / 12f);
            if (z > far) h += 0.9f + 9f * Smooth(56f, 58.5f, z + 3f * (Mathf.PerlinNoise(x * 0.08f, 4f) - 0.5f));
            // The chasm: sheer walls, deep and dark.
            float inside = Smooth(near - 0.5f, near + 0.4f, z) * (1f - Smooth(far - 0.4f, far + 0.5f, z));
            h = Mathf.Lerp(h, -40f + fine * 4f, inside);
            // The frozen lake, a shallow hollow.
            float lake = 1f - Smooth(0.7f, 1f, new Vector2((x + 31f) / 14f, (z + 24f) / 9f).magnitude);
            h -= lake * 0.6f;
            // Mountains round the edge.
            float sides = Mathf.Max(Smooth(62f, 98f, Mathf.Abs(x)), Smooth(-58f, -88f, z));
            h += sides * (34f + 22f * Mathf.PerlinNoise(x * 0.03f + 11f, z * 0.03f + 2f));
            // Lower ridges far ahead, so the dying sun shows over them.
            h += Smooth(112f, 150f, z) * (1f - sides) * (10f + 9f * Mathf.PerlinNoise(x * 0.05f + 4f, 1.5f));
            return h;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float LakeAmount(float x, float z) => 1f - Smooth(0.7f, 1f, new Vector2((x + 31f) / 14f, (z + 24f) / 9f).magnitude);

        static Mesh MakeGround()
        {
            const float minX = -110f, maxX = 110f, minZ = -100f, maxZ = 160f, step = 1f;
            int nx = Mathf.RoundToInt((maxX - minX) / step) + 1, nz = Mathf.RoundToInt((maxZ - minZ) / step) + 1;
            var vertices = new Vector3[nx * nz];
            var colours = new Color[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = minX + i * step, z = minZ + j * step;
                    vertices[j * nx + i] = new Vector3(x, Height(x, z), z);
                    colours[j * nx + i] = new Color(LakeAmount(x, z) > 0.3f ? 1f : 0f, 0f, 0f, 1f);
                }
            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            return SaveMesh("Frozen Ground", mesh =>
            {
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.colors = colours;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        /// <summary>A flat ring in the XZ plane, uv.x around it and uv.y from the inside out.</summary>
        static Mesh MakeRing(string name, int segments, float inner, float outer)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(dir * inner * 0.5f);
                vertices.Add(dir * outer * 0.5f);
                uvs.Add(new Vector2((float)s / segments, 0f));
                uvs.Add(new Vector2((float)s / segments, 1f));
                if (s == segments) continue;
                int i = s * 2;
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 2, i + 1, i + 3 });
            }
            return SaveMesh(name, mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(outer, 0.1f, outer));
            });
        }

        /// <summary>A lumpy rock about a unit across: a sphere pushed in and out by noise, faceted.</summary>
        static Mesh MakeRock(string name, int seed, float roughness)
        {
            var source = new List<Vector3>();
            const int rings = 8, segments = 12;
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float theta = Mathf.PI * r / rings, phi = 2f * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float bump = 1f + roughness * (Mathf.PerlinNoise(n.x * 1.7f + seed, n.y * 1.7f + n.z * 0.9f + seed * 0.37f) - 0.5f) * 2f;
                    var p = n * 0.5f * bump;
                    p.y *= 0.72f;
                    source.Add(p);
                }
            // Each triangle its own corners, so the facets show.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    foreach (int k in new[] { a, a + 1, b, b, a + 1, b + 1 })
                    {
                        triangles.Add(vertices.Count);
                        vertices.Add(source[k]);
                    }
                }
            return SaveMesh(name, mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        // ---- The traveller ----

        struct Figure
        {
            public Transform Body, Hips, Spine, Chest, Neck, Head, Cloak;
            public TravellerBody.Side Left, Right;
        }

        /// <summary>
        /// A jointed figure about 1.8 m tall: porcelain limbs on dark ball joints, a featureless head
        /// in a hood, a long cloak from the shoulders, a frame on the back. Pivots sit at the joints so
        /// <see cref="TravellerBody"/> can turn them.
        /// </summary>
        static Figure MakeFigure(Transform root, int layer, Material porcelain, Material joint, Material cloth, Material packMaterial)
        {
            var f = new Figure();
            f.Body = Joint("Body", root, Vector3.zero, layer);
            f.Hips = Joint("Hips", f.Body, new Vector3(0f, 0.99f, 0f), layer);
            Shape("Pelvis", f.Hips, Ovoid("Pelvis", 0.17f, 0.11f, 0.12f), porcelain, new Vector3(0f, 0.02f, 0f), layer);
            f.Spine = Joint("Spine", f.Hips, new Vector3(0f, 0.1f, 0f), layer);
            Shape("Waist", f.Spine, Limb("Waist", 0.24f, 0.115f, 0.135f, true), porcelain, Vector3.zero, layer);
            f.Chest = Joint("Chest", f.Spine, new Vector3(0f, 0.21f, 0f), layer);
            Shape("Ribs", f.Chest, Ovoid("Ribs", 0.2f, 0.17f, 0.135f), porcelain, new Vector3(0f, 0.12f, 0f), layer);
            f.Neck = Joint("Neck", f.Chest, new Vector3(0f, 0.28f, 0f), layer);
            Shape("Neck", f.Neck, Limb("Neck", 0.09f, 0.045f, 0.05f, true), joint, Vector3.zero, layer);
            f.Head = Joint("Head", f.Neck, new Vector3(0f, 0.08f, 0f), layer);
            Shape("Head", f.Head, Ovoid("Head", 0.095f, 0.125f, 0.112f), porcelain, new Vector3(0f, 0.1f, 0.012f), layer);
            Shape("Hood", f.Head, Hood(), cloth, new Vector3(0f, 0.1f, -0.01f), layer);
            Shape("Satchel", f.Chest, Ovoid("Satchel", 0.15f, 0.19f, 0.07f), packMaterial, new Vector3(0f, 0.16f, -0.19f), layer);

            foreach (float side in new[] { -1f, 1f })
            {
                var s = new TravellerBody.Side();
                string n = side < 0f ? "Left" : "Right";
                s.upperArm = Joint($"Upper Arm {n}", f.Chest, new Vector3(side * 0.215f, 0.2f, 0f), layer);
                Shape("Shoulder", s.upperArm, Ovoid("Ball", 0.06f, 0.06f, 0.06f), joint, Vector3.zero, layer);
                Shape("Arm", s.upperArm, Limb("Upper Arm", 0.29f, 0.056f, 0.045f, false), porcelain, new Vector3(0f, -0.02f, 0f), layer);
                s.forearm = Joint($"Forearm {n}", s.upperArm, new Vector3(0f, -0.31f, 0f), layer);
                Shape("Elbow", s.forearm, Ovoid("Small Ball", 0.045f, 0.045f, 0.045f), joint, Vector3.zero, layer);
                Shape("Forearm", s.forearm, Limb("Forearm", 0.25f, 0.045f, 0.036f, false), porcelain, new Vector3(0f, -0.015f, 0f), layer);
                Shape("Hand", s.forearm, Ovoid("Hand", 0.045f, 0.085f, 0.032f), porcelain, new Vector3(0f, -0.32f, 0.01f), layer);
                s.thigh = Joint($"Thigh {n}", f.Hips, new Vector3(side * 0.1f, -0.05f, 0f), layer);
                Shape("Thigh", s.thigh, Limb("Thigh", 0.43f, 0.08f, 0.06f, false), porcelain, Vector3.zero, layer);
                s.shin = Joint($"Shin {n}", s.thigh, new Vector3(0f, -0.45f, 0f), layer);
                Shape("Knee", s.shin, Ovoid("Small Ball", 0.045f, 0.045f, 0.045f), joint, Vector3.zero, layer);
                Shape("Shin", s.shin, Limb("Shin", 0.42f, 0.055f, 0.04f, false), porcelain, new Vector3(0f, -0.01f, 0f), layer);
                s.foot = Joint($"Foot {n}", s.shin, new Vector3(0f, -0.44f, 0f), layer);
                Shape("Foot", s.foot, Ovoid("Foot", 0.055f, 0.042f, 0.13f), joint, new Vector3(0f, -0.035f, 0.055f), layer);
                if (side < 0f) f.Left = s;
                else f.Right = s;
            }
            f.Cloak = Joint("Cloak", f.Chest, new Vector3(0f, 0.25f, -0.01f), layer);
            Shape("Cloak", f.Cloak, Cloak(), cloth, Vector3.zero, layer);
            return f;
        }

        static Transform Joint(string name, Transform parent, Vector3 at, int layer)
        {
            var joint = new GameObject(name) { layer = layer }.transform;
            joint.SetParent(parent, false);
            joint.localPosition = at;
            return joint;
        }

        static void Shape(string name, Transform parent, Mesh mesh, Material material, Vector3 at, int layer)
        {
            var part = new GameObject(name) { layer = layer };
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        static Mesh Ovoid(string name, float rx, float ry, float rz)
        {
            const int rings = 16, segments = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float theta = Mathf.PI * r / rings, phi = 2f * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(new Vector3(n.x * rx, n.y * ry, n.z * rz));
                    normals.Add(new Vector3(n.x / rx, n.y / ry, n.z / rz).normalized);
                }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return SaveMesh("Figure " + name, mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            });
        }

        /// <summary>A tapered capsule hanging down from its pivot (or standing up from it).</summary>
        static Mesh Limb(string name, float length, float top, float bottom, bool up)
        {
            const int segments = 20, rings = 14;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            // A profile from the top cap through the shaft to the bottom cap.
            var profile = new List<(float y, float r, float ny)>();
            for (int i = 0; i <= 4; i++)
            {
                float a = Mathf.PI * 0.5f * (1f - i / 4f);
                profile.Add((Mathf.Sin(a) * top, Mathf.Cos(a) * top, Mathf.Sin(a)));
            }
            for (int i = 1; i < rings; i++)
            {
                float t = i / (float)rings;
                profile.Add((-length * t, Mathf.Lerp(top, bottom, t), 0f));
            }
            for (int i = 0; i <= 4; i++)
            {
                float a = Mathf.PI * 0.5f * (i / 4f);
                profile.Add((-length - Mathf.Sin(a) * bottom, Mathf.Cos(a) * bottom, -Mathf.Sin(a)));
            }
            for (int p = 0; p < profile.Count; p++)
                for (int s = 0; s <= segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    var (y, r, ny) = profile[p];
                    var around = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    var position = around * r + Vector3.up * y;
                    var normal = (around * Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) + Vector3.up * ny).normalized;
                    if (up)
                    {
                        position.y = -position.y;
                        normal.y = -normal.y;
                    }
                    vertices.Add(position);
                    normals.Add(normal);
                }
            for (int p = 0; p < profile.Count - 1; p++)
                for (int s = 0; s < segments; s++)
                {
                    int a = p * (segments + 1) + s, b = a + segments + 1;
                    if (up) triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    else triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return SaveMesh("Figure " + name, mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            });
        }

        /// <summary>A long cloak hanging from the shoulders, in folds that deepen toward the hem, open in front below the waist.</summary>
        static Mesh Cloak()
        {
            const int around = 48, down = 22;
            const float length = 1.12f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int j = 0; j <= down; j++)
            {
                float t = j / (float)down;
                float rx = Mathf.Lerp(0.25f, 0.43f, Mathf.Pow(t, 0.8f));
                float rz = Mathf.Lerp(0.16f, 0.34f, Mathf.Pow(t, 0.8f));
                for (int i = 0; i <= around; i++)
                {
                    float a = 2f * Mathf.PI * i / around;
                    float fold = 1f + (0.015f + 0.07f * t) * Mathf.Sin(a * 9f + t * 2f);
                    vertices.Add(new Vector3(Mathf.Sin(a) * rx * fold, -length * t + (t > 0.95f ? 0.02f * Mathf.Sin(a * 13f) : 0f), -Mathf.Cos(a) * rz * fold));
                }
            }
            for (int j = 0; j < down; j++)
                for (int i = 0; i < around; i++)
                {
                    float t = (j + 0.5f) / down;
                    float a = 2f * Mathf.PI * (i + 0.5f) / around;
                    // The front (a near pi) is open from the waist down, wider toward the hem.
                    float open = t < 0.3f ? 0f : Mathf.Lerp(0.15f, 0.62f, (t - 0.3f) / 0.7f);
                    if (Mathf.Abs(a - Mathf.PI) < open) continue;
                    int p = j * (around + 1) + i, q = p + around + 1;
                    triangles.AddRange(new[] { p, p + 1, q, p + 1, q + 1, q });
                }
            return SaveMesh("Figure Cloak", mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        /// <summary>A hood round the head, open at the face, its point falling behind.</summary>
        static Mesh Hood()
        {
            const int rings = 14, segments = 32;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float theta = Mathf.PI * 0.62f * r / rings, phi = 2f * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), -Mathf.Sin(theta) * Mathf.Cos(phi));
                    float back = Mathf.Max(0f, -n.z);
                    vertices.Add(new Vector3(n.x * 0.135f, n.y * 0.16f + 0.01f, n.z * 0.145f - back * back * 0.06f * (1f - n.y)));
                }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * (s + 0.5f) / segments;
                    float theta = Mathf.PI * 0.62f * (r + 0.5f) / rings;
                    // The face opening: the front (phi near pi) below the brow.
                    if (Mathf.Abs(phi - Mathf.PI) < 0.95f && theta > 0.5f) continue;
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return SaveMesh("Figure Hood", mesh =>
            {
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        // ---- Things ----

        static Landmark MakeLandmark(string name, Transform parent, Vector3 at, string id, string title, string about, Vector3 colliderSize, bool collide = true)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = at;
            var landmark = holder.AddComponent<Landmark>();
            landmark.Set(id, title, about);
            if (collide && colliderSize != Vector3.zero)
            {
                // An invisible box the centre of the view can rest on, so the place has a name.
                var box = holder.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = colliderSize;
            }
            return landmark;
        }

        static void MakePickup(Transform parent, Mesh mesh, Material material, Matter matter, int units, Vector2 at, float size, System.Random random)
        {
            var piece = MakePart(Matters.Word(matter), parent, mesh, material, 1f);
            piece.transform.localScale = new Vector3(size * (0.9f + 0.3f * (float)random.NextDouble()), size * (0.8f + 0.3f * (float)random.NextDouble()), size * (0.9f + 0.3f * (float)random.NextDouble()));
            piece.transform.position = new Vector3(at.x, Height(at.x, at.y) + size * 0.22f, at.y);
            piece.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 30f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 30f);
            piece.shadowCastingMode = ShadowCastingMode.On;
            piece.receiveShadows = true;
            var collider = piece.gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
            piece.gameObject.AddComponent<Pickup>().Set(matter, units);
        }

        static Ember MakeEmber(string name, Transform parent, Vector3 at, Mesh sphere, Mesh billboard, Material coreMaterial, Material glowMaterial)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = at;
            var core = MakePart("Core", holder.transform, sphere, coreMaterial, 0.32f);
            var glow = MakePart("Glow", holder.transform, billboard, glowMaterial, 3.2f);
            var shine = new GameObject("Shine").AddComponent<Light>();
            shine.transform.SetParent(holder.transform, false);
            shine.type = LightType.Point;
            shine.color = new Color(1f, 0.55f, 0.25f);
            shine.range = 12f;
            shine.intensity = 3.5f;
            shine.shadows = LightShadows.None;
            var hum = MakeSource("Hum", holder.transform, FindClip("ember_hum") ?? FindClip("near_orb_loop"), true);
            hum.spatialBlend = 1f;
            hum.minDistance = 2f;
            hum.maxDistance = 30f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.playOnAwake = true;
            hum.volume = 0.35f;
            var ember = holder.AddComponent<Ember>();
            Wire(ember, ("core", core), ("glow", glow), ("shine", shine), ("hum", hum));
            return ember;
        }

        /// <summary>
        /// A square ruin, ten metres a side and four and a half high, of the last builders' dark metal.
        /// Its door, in the wall facing the chasm, sinks into the ground while enough weight rests on
        /// the plate before it. The ember waits inside.
        /// </summary>
        static (Gate, Ember) MakeRuin(Transform parent, Vector3 centre, Mesh cube, Material wallMaterial, Material plateMaterial, Material lightMaterial,
            Mesh sphere, Mesh billboard, Material emberCore, Material emberGlow, List<Landmark> landmarks, Transform traveller)
        {
            const float side = 10f, height = 4.5f, thick = 0.6f, door = 2.4f;
            var root = new GameObject("Walled Ruin").transform;
            root.SetParent(parent, false);
            float baseY = Height(centre.x, centre.z);
            root.position = new Vector3(centre.x, baseY, centre.z);
            void Wall(string name, Vector3 at, Vector3 size)
            {
                var wall = MakePart(name, root, cube, wallMaterial, 1f);
                wall.transform.localPosition = at;
                wall.transform.localScale = size;
                wall.shadowCastingMode = ShadowCastingMode.On;
                wall.receiveShadows = true;
                wall.gameObject.AddComponent<BoxCollider>();
            }
            float y = height * 0.5f - 0.8f, tall = height + 1.6f;
            Wall("North Wall", new Vector3(0f, y, side * 0.5f), new Vector3(side + thick, tall, thick));
            Wall("East Wall", new Vector3(side * 0.5f, y, 0f), new Vector3(thick, tall, side));
            Wall("West Wall", new Vector3(-side * 0.5f, y, 0f), new Vector3(thick, tall, side));
            float piece = (side - door) * 0.5f;
            Wall("South Wall Left", new Vector3(-(door + piece) * 0.5f, y, -side * 0.5f), new Vector3(piece + thick * 0.5f, tall, thick));
            Wall("South Wall Right", new Vector3((door + piece) * 0.5f, y, -side * 0.5f), new Vector3(piece + thick * 0.5f, tall, thick));
            var doorPart = MakePart("Door", root, cube, wallMaterial, 1f);
            doorPart.transform.localPosition = new Vector3(0f, height * 0.5f - 0.3f, -side * 0.5f);
            doorPart.transform.localScale = new Vector3(door + 0.1f, height - 0.2f, thick * 0.8f);
            doorPart.shadowCastingMode = ShadowCastingMode.On;
            doorPart.receiveShadows = true;
            doorPart.gameObject.AddComponent<BoxCollider>();

            // The plate before the door, ringed with a thin line of light.
            var plateRoot = new GameObject("Plate").transform;
            plateRoot.SetParent(root, false);
            plateRoot.localPosition = new Vector3(0f, 0f, -side * 0.5f - 2.6f);
            plateRoot.position = new Vector3(plateRoot.position.x, Height(plateRoot.position.x, plateRoot.position.z), plateRoot.position.z);
            var plate = MakePart("Stone", plateRoot, cube, plateMaterial, 1f);
            plate.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            plate.transform.localScale = new Vector3(1.8f, 0.16f, 1.8f);
            plate.receiveShadows = true;
            plate.shadowCastingMode = ShadowCastingMode.On;
            plate.gameObject.AddComponent<BoxCollider>();
            foreach (var (at, size) in new[] { (new Vector3(0f, 0.13f, 0.95f), new Vector3(2f, 0.03f, 0.05f)), (new Vector3(0f, 0.13f, -0.95f), new Vector3(2f, 0.03f, 0.05f)),
                (new Vector3(0.95f, 0.13f, 0f), new Vector3(0.05f, 0.03f, 1.9f)), (new Vector3(-0.95f, 0.13f, 0f), new Vector3(0.05f, 0.03f, 1.9f)) })
            {
                var line = MakePart("Light Line", plate.transform.parent, cube, lightMaterial, 1f);
                line.transform.localPosition = at;
                line.transform.localScale = size;
            }
            var pressing = new GameObject("Pressing").AddComponent<BoxCollider>();
            pressing.transform.SetParent(plateRoot, false);
            pressing.isTrigger = true;
            pressing.center = new Vector3(0f, 0.7f, 0f);
            pressing.size = new Vector3(1.7f, 1.2f, 1.7f);
            var sound = MakeSource("Grinding", root, null, false);
            sound.spatialBlend = 0.8f;
            var gate = root.gameObject.AddComponent<Gate>();
            Wire(gate, ("door", doorPart.transform), ("plate", plate.transform), ("pressing", pressing), ("traveller", traveller), ("sound", sound),
                ("moveClip", FindClip("build_apart_2") ?? FindClip("build_lock_stone_2")));

            var ember = MakeEmber("Ruin Ember", root, root.position + new Vector3(0f, 1.6f, 1f), sphere, billboard, emberCore, emberGlow);
            landmarks.Add(MakeLandmark("Ember Place", ember.transform, ember.transform.position, "EMBER_2", "an ember of light", "the second of the three; it is inside the walled ruin", Vector3.one * 1.2f));
            landmarks.Add(MakeLandmark("Ruin", root, root.position + Vector3.up * 2f, "RUIN", "the walled ruin",
                $"a square ruin {side:F0} m across with walls {height} m high and no roof; its only door faces the chasm and is shut", new Vector3(side + 1.5f, height + 1f, side + 1.5f)));
            landmarks.Add(MakeLandmark("Door", doorPart.transform, doorPart.transform.position, "DOOR", "the stone door", "the ruin's door, shut; it sinks into the ground while something heavy rests on the plate before it", new Vector3(1f, 1f, 1.6f)));
            landmarks.Add(MakeLandmark("Plate Place", plateRoot, plateRoot.position, "PLATE", "the plate", "a square stone plate in the ground before the door; the traveller alone is too light to press it down", new Vector3(2f, 0.6f, 2f)));
            return (gate, ember);
        }

        static void Snow(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.startColor = new Color(0.9f, 0.92f, 1f, 0.8f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = particles.emission;
            emission.rateOverTime = 260f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 14f, 36f);
            shape.position = new Vector3(0f, 3f, 10f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
        }

        static Material MakeMatter(string name, Color colour, Color variation, float smoothness, float metallic, Color edge, Color rim, Color emission) =>
            MakeMaterial(name, "Game/Matter", m =>
            {
                m.SetColor("_BaseColor", colour);
                m.SetColor("_Variation", variation);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                m.SetColor("_EdgeColor", edge);
                m.SetColor("_Glow", rim);
                m.SetColor("_Emission", emission);
            });

        static PhysicsMaterial MakePhysics(string name, float friction)
        {
            string path = $"{Root}/Materials/{name}.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
            }
            material.staticFriction = friction;
            material.dynamicFriction = friction;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static VolumeProfile MakeProfile()
        {
            string path = $"{Root}/Settings/Last Light Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(true);
            AddOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var grading = AddOverride<ColorAdjustments>(profile);
            grading.postExposure.Override(0.85f);
            grading.contrast.Override(14f);
            grading.saturation.Override(-6f);
            var balance = AddOverride<WhiteBalance>(profile);
            balance.temperature.Override(-6f);
            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            var grain = AddOverride<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.16f);
            AddOverride<ChromaticAberration>(profile).intensity.Override(0.04f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ---- Wiring ----

        static void SetMask(Object target, string field, int mask)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no field '{field}'")).intValue = mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSide(Object target, string field, TravellerBody.Side side)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no field '{field}'");
            property.FindPropertyRelative("upperArm").objectReferenceValue = side.upperArm;
            property.FindPropertyRelative("forearm").objectReferenceValue = side.forearm;
            property.FindPropertyRelative("thigh").objectReferenceValue = side.thigh;
            property.FindPropertyRelative("shin").objectReferenceValue = side.shin;
            property.FindPropertyRelative("foot").objectReferenceValue = side.foot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
