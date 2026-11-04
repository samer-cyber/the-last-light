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
    /// Builds the Last Light from nothing: the frozen valley with its chasm, cliff, lake and mountains,
    /// the dimmed red sun and the dark thing in the sky, the vault where the last people sleep, the
    /// survivor (a jointed figure in a cloak), the camera at their shoulder, the drone with its eye,
    /// the old station with the last heart frozen in ice and the maker's writing on its wall, the sun
    /// mirror on the clifftop, the maker, the mind, the voice and the director, all wired together.
    /// Running it again replaces the scene.
    /// </summary>
    public static partial class LastLightSceneBuilder
    {
        const string ScenePath = Root + "/Scenes/LastLight.unity";
        static readonly Vector3 SunDirection = new Vector3(-0.38f, 0.16f, 0.91f).normalized;
        static readonly Vector3 HoleDirection = new Vector3(0.55f, 0.52f, 0.65f).normalized;

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
            var irisRing = MakeRing("Iris Ring", 48, 0.58f, 1f);
            var rocks = Enumerable.Range(0, 4).Select(i => MakeRock($"Rock {i}", 11 + i * 7, 0.28f)).ToArray();
            var monolith = MakeRock("Monolith", 21, 0.12f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            // ---- Materials ----
            var sky = MakeMaterial("Dying Sky", "Game/DyingSky", m => m.SetVector("_SunDirection", SunDirection));
            var groundMaterial = MakeMaterial("Frozen Ground", "Game/Ground", m =>
            {
                m.SetColor("_Snow", new Color(0.8f, 0.83f, 0.9f));
                m.SetColor("_SnowShade", new Color(0.55f, 0.6f, 0.74f));
            });
            var stone = MakeMatter("Matter Stone", new Color(0.5f, 0.48f, 0.47f), new Color(0.33f, 0.32f, 0.33f), 0.18f, 0f, new Color(0.55f, 0.54f, 0.52f), Color.black, Color.black);
            var ice = MakeMatter("Matter Ice", new Color(0.42f, 0.6f, 0.72f), new Color(0.28f, 0.44f, 0.58f), 0.9f, 0f, new Color(0.75f, 0.88f, 0.95f), new Color(0.12f, 0.3f, 0.45f), Color.black);
            var metal = MakeMatter("Matter Metal", new Color(0.3f, 0.29f, 0.3f), new Color(0.16f, 0.15f, 0.17f), 0.62f, 0.85f, new Color(0.62f, 0.58f, 0.52f), Color.black, Color.black);
            var glass = MakeMatter("Matter Glass", new Color(0.55f, 0.7f, 0.74f), new Color(0.45f, 0.6f, 0.66f), 0.97f, 0.15f, new Color(0.9f, 0.97f, 1f), new Color(0.35f, 0.55f, 0.65f), Color.black);
            var wood = MakeMatter("Matter Wood", new Color(0.36f, 0.24f, 0.15f), new Color(0.22f, 0.14f, 0.08f), 0.28f, 0f, new Color(0.46f, 0.33f, 0.21f), Color.black, Color.black);
            var light = MakeMatter("Matter Light", new Color(1f, 0.9f, 0.75f), new Color(1f, 0.8f, 0.6f), 0.5f, 0f, new Color(1f, 0.95f, 0.85f), new Color(1.2f, 0.8f, 0.5f), new Color(2.4f, 1.7f, 1.1f));
            // In the order the model names them (Mind.Looks): stone, metal, ice, glass, wood, light.
            var looks = new[] { stone, metal, ice, glass, wood, light };
            var oldIce = MakeMatter("Old Ice", new Color(0.56f, 0.76f, 0.88f), new Color(0.4f, 0.6f, 0.76f), 0.95f, 0f, new Color(0.85f, 0.95f, 1f), new Color(0.3f, 0.6f, 0.85f), new Color(0.04f, 0.09f, 0.14f));
            var concrete = MakeMatter("Vault Concrete", new Color(0.3f, 0.3f, 0.31f), new Color(0.22f, 0.22f, 0.24f), 0.12f, 0f, new Color(0.42f, 0.42f, 0.42f), Color.black, Color.black);
            var ghost = MakeMaterial("Plan Ghost", "Game/Ghost", m =>
            {
                m.SetColor("_Color", new Color(1.3f, 0.95f, 0.65f));
                m.SetFloat("_Intensity", 0.42f);
                m.SetFloat("_Edge", 1.1f);
                m.SetFloat("_Rim", 0.45f);
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
            var droneGlow = MakeMaterial("Drone Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.62f, 0.3f));
                m.SetFloat("_Edge", 0.12f);
                m.SetFloat("_Intensity", 0.5f);
            });
            var iris = MakeMaterial("Drone Iris", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetColor("_BaseColor", new Color(2.2f, 1.36f, 0.55f));
                m.SetFloat("_Cull", 0f);
            });
            var pupil = MakeMaterial("Drone Pupil", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f)));
            var snowMaterial = MakeParticleMaterial("Falling Snow", softDot, 0.35f);
            var flame = MakeParticleMaterial("Flame", softDot, 1.8f);
            var beamLine = MakeParticleMaterial("Drone Beam", softLine, 2.4f);
            var steamMaterial = MakeParticleMaterial("Steam", softDot, 0.5f);
            var shaft = MakeMaterial("Mirror Beam", "Game/Beam", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.72f, 0.42f));
                m.SetFloat("_Intensity", 0f);
                m.SetFloat("_Softness", 2.2f);
            });
            var writing = MakeMaterial("Maker Message", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>($"{Root}/Textures/Maker Message.png"));
                m.SetColor("_BaseColor", new Color(0.92f, 0.88f, 0.82f));
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.35f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
            });
            var warmWindow = MakeMaterial("Vault Window", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(1.6f, 0.75f, 0.32f)));
            var porcelain = MakeLit("Traveller Porcelain", new Color(0.78f, 0.76f, 0.72f), 0.62f, 0f);
            var joint = MakeLit("Traveller Joint", new Color(0.09f, 0.09f, 0.1f), 0.55f, 0.75f);
            var cloth = MakeLit("Traveller Cloak", new Color(0.05f, 0.055f, 0.065f), 0.18f, 0f);
            cloth.SetFloat("_Cull", 0f);
            cloth.doubleSidedGI = true;
            var pack = MakeLit("Traveller Pack", new Color(0.13f, 0.12f, 0.11f), 0.35f, 0.3f);
            var visor = MakeLit("Drone Visor", new Color(0.015f, 0.015f, 0.02f), 0.95f, 0.3f);
            var slippery = MakePhysics("Slippery", 0.04f);
            var bouncy = MakePhysics("Bouncy", 0.6f, 0.85f);

            var profile = MakeProfile();
            var panel = MakePanel();

            // ---- The scene ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.2f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.12f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.085f, 0.05f, 0.05f);
            RenderSettings.fogDensity = 0.0032f;
            int travellerLayer = EnsureLayer("Traveller");
            int droneLayer = EnsureLayer("Drone");
            int notPicked = ~(1 << travellerLayer) & ~(1 << droneLayer) & ~(1 << 2);

            // The dimmed sun: huge and red on the horizon, its light taken; the scene's main light.
            var sunObject = new GameObject("Dying Sun");
            sunObject.transform.position = SunDirection * 3200f;
            MakePart("Body", sunObject.transform, sphere, sunMaterial, 1100f);
            MakePart("Glow", sunObject.transform, billboard, sunGlow, 1100f * 3.2f);
            var sunlight = new GameObject("Sunlight").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sunlight.color = new Color(1f, 0.42f, 0.24f);
            sunlight.intensity = 2.5f;
            sunlight.shadows = LightShadows.Soft;
            sunlight.shadowStrength = 0.85f;
            RenderSettings.sun = sunlight;

            // The Lattice: the great minds' black net round the sun.
            MakeLattice(quad, SunDirection);

            // The ground.
            var groundObject = new GameObject("Ground");
            groundObject.AddComponent<MeshFilter>().sharedMesh = ground;
            var groundRenderer = groundObject.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            groundObject.AddComponent<MeshCollider>().sharedMesh = ground;
            float startY = Height(Start.x, Start.z);

            // ---- The survivor ----
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
            // Where things ride: in front, between both hands; and in the right fist, pointing on from the forearm.
            var hands = Joint("Hands", figure.Body, new Vector3(0f, 1.02f, 0.46f), travellerLayer);
            var hand = Joint("Hand", figure.Right.forearm, new Vector3(0f, -0.33f, 0.03f), travellerLayer);
            hand.localRotation = Quaternion.Euler(90f, 0f, 0f);

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
            SetMask(follow, "solid", notPicked);

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
            var handsSound = MakeSource("Hands", travellerObject.transform, null, false);

            // ---- The drone ----
            var drone = MakeDrone(travellerObject.transform, figure.Body, follow, droneLayer, sphere, irisRing, cube, billboard, porcelain, visor, joint, iris, pupil, droneGlow, beamLine, camera);

            // ---- The interface ----
            var hudStyle = Load<StyleSheet>($"{Root}/UI/Hud.uss");
            var sayStyle = Load<StyleSheet>($"{Root}/UI/Say.uss");
            var interfaceObject = new GameObject("Interface");
            var document = interfaceObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var hud = interfaceObject.AddComponent<Hud>();
            Wire(hud, ("document", document), ("style", hudStyle));

            // ---- The survivor's parts, now that everything they talk to exists ----
            var traveller = travellerObject.AddComponent<Traveller>();
            Wire(traveller, ("follow", follow), ("drone", drone), ("hud", hud), ("body", figure.Body), ("hands", hands), ("hand", hand), ("sound", handsSound),
                ("pickClip", FindClip("pick_up") ?? FindClip("foley_pickup_stone_1")), ("dropClip", FindClip("put_down")),
                ("strikeClip", FindClip("pick_strike")), ("torchClip", FindClip("torch_loop")));
            SetMask(traveller, "seen", notPicked);
            Wire(drone, ("traveller", traveller));
            var body = figure.Body.gameObject.AddComponent<TravellerBody>();
            Wire(body, ("traveller", traveller), ("hips", figure.Hips), ("spine", figure.Spine), ("chest", figure.Chest), ("neck", figure.Neck),
                ("head", figure.Head), ("cloak", figure.Cloak), ("lookAt", drone.transform));
            SetSide(body, "left", figure.Left);
            SetSide(body, "right", figure.Right);
            // The survivor's look: a rigged model whose skeleton follows the figure's joints.
            Dress(figure, hand, travellerLayer, "CryoMan", 1.78f);

            var say = interfaceObject.AddComponent<SayLine>();
            Wire(say, ("document", document), ("style", sayStyle), ("traveller", traveller), ("follow", follow), ("sound", typeSound));
            WireArray(say, "keyClips", Enumerable.Range(1, 6).Select(i => (Object)FindClip($"pencil_stroke_{i}")).Where(c => c != null).ToArray());
            var talk = interfaceObject.AddComponent<PushToTalk>();
            Wire(talk, ("hud", hud), ("sound", talkSound), ("startClip", FindClip("companion_listen") ?? FindClip("listen_on")), ("stopClip", FindClip("listen_off")));

            // ---- The world's things ----
            var landmarks = new List<Landmark>();
            var world = new GameObject("World").transform;
            var frost = MakeLit("Frost", new Color(0.72f, 0.82f, 0.92f), 0.7f, 0f);
            var vault = MakeVault(world, cube, cylinder, ball, concrete, metal, warmWindow, glass, frost, light, ice, steamMaterial, landmarks);
            var vaultLight = vault.Light;
            var vaultDoor = vault.Door;
            // The survivor wakes in their pod, at the far end of the hall.
            travellerObject.transform.position = vault.PodStand + Vector3.up * 0.05f;
            figure.Body.rotation = Quaternion.LookRotation(vault.PodFacing, Vector3.up);
            cameraObject.transform.SetPositionAndRotation(vault.PodStand + Vector3.up * 1.6f - vault.PodFacing * 0.1f, Quaternion.LookRotation(vault.PodFacing, Vector3.up));
            drone.transform.position = vault.PodStand + vault.PodFacing * 1.5f + Vector3.up * 1.5f;

            // The frozen town.
            var snowCover = MakeLit("Snow Cover", new Color(0.78f, 0.8f, 0.86f), 0.25f, 0f);
            var roofMetal = MakeLit("Roof Metal", new Color(0.16f, 0.14f, 0.14f), 0.35f, 0.6f);
            var posterTexture = Load<Texture2D>($"{Root}/Textures/Lumen Poster.jpg") ?? Load<Texture2D>($"{Root}/Textures/Lumen Poster.png");
            var posterMaterial = MakeMaterial("Lumen Poster", "Universal Render Pipeline/Lit", m =>
            {
                if (posterTexture) m.SetTexture("_BaseMap", posterTexture);
                m.SetColor("_BaseColor", posterTexture ? new Color(0.75f, 0.72f, 0.7f) : new Color(0.2f, 0.18f, 0.16f));
                m.SetFloat("_Smoothness", 0.15f);
            });
            MakeTown(world, cube, cylinder, quad, concrete, roofMetal, snowCover, visor, metal, posterMaterial, landmarks);

            // The chasm, all the way across the valley, and the far side.
            float chasmMid = (ChasmNear + ChasmFar) * 0.5f;
            landmarks.Add(MakeLandmark("Chasm", world, new Vector3(0f, -20f, chasmMid), "CHASM", "the chasm",
                $"{ChasmFar - ChasmNear:F0} m wide and 45 m deep, right across the valley; nothing crosses it", new Vector3(600f, 44f, ChasmFar - ChasmNear - 0.6f)));
            float farY = Height(0f, ChasmFar + 6f);
            var farSide = MakeLandmark("Far Side", world, new Vector3(0f, farY, ChasmFar + 6f), "FAR_SIDE", "the far side", "the ground across the chasm, where the station stands", Vector3.zero);
            var farRegion = farSide.gameObject.AddComponent<BoxCollider>();
            farRegion.isTrigger = true;
            farRegion.center = new Vector3(0f, 0f, 40f);
            farRegion.size = new Vector3(620f, 30f, 84f);
            landmarks.Add(farSide);

            // The old station: the plate and the door, the heart in its ice, the writing on the wall.
            var station = MakeStation(world, new Vector3(StationAt.x, 0f, StationAt.y), cube, cylinder, quad, sphere, metal, stone, light, oldIce, writing, steamMaterial, landmarks, travellerObject.transform);
            if (station.Case)
            {
                Object.DestroyImmediate(station.Case.gameObject);
                landmarks.RemoveAll(l => !l);
            }
            // A loose block of the station's stone, heavy enough for the plate, for anyone who can lift it.
            var oldBlock = MakePart("Old Block", world, cube, stone, 0.85f);
            float blockX = StationAt.x - 11f, blockZ = StationAt.y - 9f;
            float blockY = Height(blockX, blockZ);
            oldBlock.transform.position = new Vector3(blockX, blockY + 0.43f, blockZ);
            oldBlock.transform.rotation = Quaternion.Euler(0f, 23f, 0f);
            oldBlock.shadowCastingMode = ShadowCastingMode.On;
            oldBlock.gameObject.AddComponent<BoxCollider>();
            var oldBody = oldBlock.gameObject.AddComponent<Rigidbody>();
            oldBody.mass = 1600f;
            oldBody.interpolation = RigidbodyInterpolation.Interpolate;
            var oldMark = MakeLandmark("Old Block Place", oldBlock.transform, oldBlock.transform.position, "OLD_BLOCK", "an old stone block", "a loose block of the station's stone, about 1600 kg; too heavy for the survivor to move", Vector3.one * 1.1f);
            landmarks.Add(oldMark);

            // The cliff, and the sun mirror on its top.
            float topY = Height(MirrorAt.x, MirrorAt.y);
            float footY = Height(MirrorAt.x, CliffZ - 8f);
            landmarks.Add(MakeLandmark("Cliff", world, new Vector3(MirrorAt.x, footY + (topY - footY) * 0.5f, CliffZ), "CLIFF", "the cliff",
                $"a sheer wall of rock about {topY - footY:F0} m high, right across the valley; too steep to climb", new Vector3(300f, topY - footY, 6f)));
            landmarks.Add(MakeLandmark("Cliff Top", world, new Vector3(MirrorAt.x, topY, CliffZ + 9f), "CLIFF_TOP", "the top of the cliff",
                $"a shelf {topY - footY:F0} m above the ground below the cliff, where the sun mirror stands", new Vector3(300f, 4f, 12f)));
            var sunShaft = MakeParticleMaterial("Mirror Shaft", softLine, 2.2f);
            var mirrorParts = MakeMirror(world, new Vector3(MirrorAt.x, topY, MirrorAt.y), cube, cylinder, sphere, metal, oldIce, light, sunShaft, steamMaterial, vaultDoor, landmarks, travellerObject.transform);

            // The great minds' slabs, dead now, and a few boulders.
            var slabs = new GameObject("Slabs").transform;
            slabs.SetParent(world, false);
            for (int i = 0; i < 4; i++)
            {
                var slab = MakePart($"Slab {i}", slabs, monolith, metal, 1f);
                var at = new Vector2(92f + 5f * i, 40f + 3f * Mathf.Sin(i * 2.1f));
                slab.transform.position = new Vector3(at.x, Height(at.x, at.y) + 1.2f, at.y);
                slab.transform.localScale = new Vector3(1.4f, 4.5f + i, 0.6f);
                slab.transform.rotation = Quaternion.Euler(8f * Mathf.Sin(i * 3f), 20f * i, 12f * Mathf.Cos(i * 2f));
                slab.shadowCastingMode = ShadowCastingMode.On;
                slab.receiveShadows = true;
                slab.gameObject.AddComponent<MeshCollider>().sharedMesh = monolith;
            }
            landmarks.Add(MakeLandmark("Slabs Place", slabs, new Vector3(99f, Height(99f, 40f), 40f), "SLABS", "the dark slabs",
                "tall slabs the great minds left behind; dead and cold now", new Vector3(18f, 6f, 8f)));
            landmarks.Add(MakeLandmark("Lake Place", world, new Vector3(Lake.x, Height(Lake.x, Lake.y), Lake.y), "FROZEN_LAKE", "the frozen lake", "the town's lake, frozen solid", new Vector3(60f, 1f, 40f)));
            var random = new System.Random(42);
            var boulders = new GameObject("Boulders").transform;
            var spots = new List<Vector2>();
            while (spots.Count < 46)
            {
                var spot = new Vector2((float)random.NextDouble() * 440f - 220f, (float)random.NextDouble() * 330f - 40f);
                bool town = spot.x > -80f && spot.x < 80f && spot.y > TownNear - 5f && spot.y < TownFar + 5f;
                bool chasm = spot.y > ChasmNear - 4f && spot.y < ChasmFar + 4f;
                bool path = Mathf.Abs(spot.x) < 10f && spot.y < TownNear;
                if (!town && !chasm && !path && Vector2.Distance(spot, Arena) > 40f && Mathf.Abs(spot.y - CliffZ) > 5f) spots.Add(spot);
            }
            foreach (var p in spots)
            {
                var rock = MakePart("Boulder", boulders, rocks[random.Next(rocks.Length)], stone, 1f);
                float size = 0.8f + 1.2f * (float)random.NextDouble();
                rock.transform.localScale = new Vector3(size * 1.2f, size * 0.8f, size);
                rock.transform.position = new Vector3(p.x, Height(p.x, p.y) + size * 0.2f, p.y);
                rock.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 20f);
                rock.shadowCastingMode = ShadowCastingMode.On;
                rock.gameObject.AddComponent<MeshCollider>().sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
            }

            // ---- The maker, the mind and the director ----
            var makerObject = new GameObject("Maker");
            var maker = makerObject.AddComponent<Maker>();
            Wire(maker, ("cube", cube), ("cylinder", cylinder), ("sphere", ball), ("ghost", ghost), ("flame", flame),
                ("slippery", slippery), ("bouncy", bouncy), ("sound", makerSound), ("doneClip", FindClip("build_complete_1")), ("apartClip", FindClip("build_apart_1")),
                ("planClip", FindClip("build_plan_1")), ("boingClip", FindClip("spring_boing")), ("liftClip", FindClip("lift_loop")), ("fireClip", FindClip("fire_loop")),
                ("swingClip", FindClip("blade_swing")), ("boltClip", FindClip("bolt_fire")));
            WireArray(maker, "looks", looks);
            WireArray(maker, "lockClips", new[] { "build_lock_stone_1", "build_lock_metal_1", "build_lock_ice_1", "build_lock_ice_2", "build_lock_stone_2", "build_plan_2" }.Select(n => (Object)FindClip(n)).ToArray());
            SetMask(maker, "ground", notPicked);

            // Spark's holograms (Muse Image), projected from its eye or laid over the view.
            var hologramMaterial = MakeMaterial("Hologram", "Game/Hologram", m =>
            {
                m.SetColor("_Tint", new Color(0.45f, 0.9f, 1f));
                m.SetFloat("_Colour", 0.35f);
                m.SetFloat("_Intensity", 1.6f);
            });
            var hologram = new GameObject("Hologram").AddComponent<Hologram>();
            Wire(hologram, ("material", hologramMaterial), ("lineMaterial", beamLine), ("sound", makerSound), ("onClip", FindClip("hologram_on") ?? FindClip("build_plan_1")));

            // The survivor's warmth (their health), and what the machines smell.
            var warmth = travellerObject.AddComponent<Warmth>();
            Wire(warmth, ("traveller", traveller), ("follow", follow), ("hud", hud), ("sound", handsSound),
                ("hitClip", FindClip("survivor_hit")), ("blockClip", FindClip("shield_block")));
            var smell = travellerObject.AddComponent<HeatSource>();
            smell.IsSurvivor = true;
            smell.Pull = 1f;

            // The great minds' machines, made when the story calls for them.
            var machineShell = MakeLit("Machine Shell", new Color(0.045f, 0.045f, 0.055f), 0.72f, 0.85f);
            var machineEye = MakeMaterial("Machine Eye", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", new Color(3f, 0.35f, 0.2f)));
            var sparkMaterial = MakeParticleMaterial("Spark", softDot, 3f);
            MakeHulks(world, ball, cylinder, cube, machineShell, machineEye, snowCover, landmarks);
            var machines = new GameObject("Machines").AddComponent<Machines>();
            Wire(machines, ("shell", machineShell), ("eyeGlow", machineEye), ("fin", joint), ("line", beamLine), ("spark", sparkMaterial), ("charge", light), ("shieldGlow", hologramMaterial),
                ("stepClip", FindClip("harvester_step")), ("bigChargeClip", FindClip("harvester_charge")), ("beamClip", FindClip("harvester_beam_loop")),
                ("ventClip", FindClip("harvester_vent")), ("bigBreakClip", FindClip("harvester_break")), ("voiceClip", FindClip("harvester_architect")),
                ("humClip", FindClip("seeker_hum_loop")), ("fireClip", FindClip("seeker_fire")), ("chargeClip", FindClip("seeker_charge")),
                ("hurtClip", FindClip("seeker_hurt")), ("breakClip", FindClip("seeker_break")));

            var mind = new GameObject("Mind").AddComponent<Mind>();
            Wire(mind, ("card", Load<TextAsset>($"{Root}/Story/Drone card.txt")));

            var director = new GameObject("Director").AddComponent<LastLightDirector>();
            Wire(director, ("traveller", traveller), ("follow", follow), ("body", body), ("drone", drone), ("maker", maker), ("mind", mind),
                ("say", say), ("talk", talk), ("hud", hud), ("hologram", hologram), ("warmth", warmth), ("machines", machines), ("gate", station.Gate), ("heart", station.Heart),
                ("inside", station.Inside), ("farSide", farRegion), ("mirrorJoint", mirrorParts.Joint), ("socket", mirrorParts.Socket), ("mirror", mirrorParts.Mirror),
                ("sunlight", sunlight), ("vaultLight", vaultLight), ("vaultDoor", vaultDoor), ("snow", snow),
                ("ambience", ambience), ("wind", windSource), ("music", music), ("steps", stepsSource), ("finaleClip", FindClip("finale_2")),
                ("jumpClip", FindClip("foley_jump_1")), ("landClip", FindClip("foley_land_1")));
            WireArray(director, "landmarks", landmarks.Cast<Object>().ToArray());
            Wire(director, ("podLid", vault.Lid), ("hall", vault.Hall), ("crackClip", FindClip("ice_crack")), ("openClip", FindClip("mirror_turn")));
            // The vault's beats: the shrieking panel and the first memory, Ima's recording, pod 9, the frozen door.
            var memory = MakeSource("Memory", director.transform, null, false);
            Wire(director, ("podPanel", vault.Panel), ("podAlarm", vault.Alarm), ("pod9", vault.Pod9), ("doorIce", vault.DoorIce), ("memory", memory),
                ("panelClip", FindClip("panel_touch")), ("swellClip", FindClip("memory_swell")), ("flashOneClip", FindClip("flash_1")),
                ("recordingClip", FindClip("recording_click")), ("imaClip", FindClip("ima_recording")));
            Wire(drone, ("curiousChirp", FindClip("spark_curious")), ("nervousChirp", FindClip("spark_nervous")), ("happyChirp", FindClip("spark_happy")),
                ("sadChirp", FindClip("spark_sad")), ("thinkingHum", FindClip("spark_thinking")));
            SetValue(director, "harvesterAt", new Vector3(Arena.x, Height(Arena.x, Arena.y) + 6.2f, Arena.y + 6f));
            WireArray(director, "snowSteps", Clips("foley_step_snow", 6));
            WireArray(director, "stoneSteps", Clips("foley_step_stone", 6));
            WireArray(director, "iceSteps", Clips("foley_step_ice", 6));

            // Everything solid in the world can be stood on and kept out of: a collider for each shape that has none
            // (light, glow, writing and windows are not solid; moving things have their own).
            foreach (var r in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<Collider>() || r.GetComponentInParent<Rigidbody>()) continue;
                string n = r.name;
                if (new[] { "Glow", "Beam", "Shaft", "Writing", "Light Line", "Window", "Band", "Flame", "Ghost", "Face" }.Any(n.Contains)) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh) r.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            // The Last Light is the game now: first in the build; the old scenes follow, unreached.
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}");
        }

        static Object[] Clips(string stem, int count) =>
            Enumerable.Range(1, count).Select(i => (Object)FindClip($"{stem}_{i}")).Where(c => c != null).ToArray();

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

        /// <summary>
        /// The drone: a white shell the size of a head (the survivor's porcelain: the same people made
        /// both), a dark visor with one round eye in it, a fin each side with a strip of the eye's
        /// light, a soft glow, a light of its own, a beam for making and carrying, and a camera behind
        /// the eye that never sees the shell.
        /// </summary>
        static Drone MakeDrone(Transform traveller, Transform travellerBody, FollowCamera follow, int layer, Mesh sphere, Mesh ring, Mesh cube, Mesh billboard,
            Material shellMaterial, Material visorMaterial, Material finMaterial, Material irisMaterial, Material pupilMaterial, Material glowMaterial, Material beamMaterial, Camera main)
        {
            var root = new GameObject("Drone") { layer = layer };
            root.transform.position = traveller.position + new Vector3(0.8f, 1.95f, -0.2f);
            var shell = Joint("Shell", root.transform, Vector3.zero, layer);
            Renderer Bit(string name, Transform parent, Mesh mesh, Material material, Vector3 at, Vector3 scale, Vector3? euler = null)
            {
                var part = MakePart(name, parent, mesh, material, 1f);
                part.gameObject.layer = layer;
                part.transform.localPosition = at;
                part.transform.localScale = scale;
                part.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
                part.shadowCastingMode = ShadowCastingMode.On;
                return part;
            }
            Bit("Body", shell, sphere, shellMaterial, Vector3.zero, Vector3.one * 0.36f);
            Bit("Band", shell, sphere, finMaterial, new Vector3(0f, 0f, -0.01f), new Vector3(0.372f, 0.05f, 0.372f));
            Bit("Visor", shell, sphere, visorMaterial, new Vector3(0f, 0.005f, 0.128f), new Vector3(0.25f, 0.24f, 0.12f));
            var irisPivot = Joint("Iris", shell, new Vector3(0f, 0.005f, 0.19f), layer);
            var irisRenderer = Bit("Ring", irisPivot, ring, irisMaterial, Vector3.zero, Vector3.one * 0.13f, new Vector3(90f, 0f, 0f));
            irisRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Bit("Pupil", irisPivot, sphere, pupilMaterial, new Vector3(0f, 0f, -0.002f), new Vector3(0.07f, 0.07f, 0.01f)).shadowCastingMode = ShadowCastingMode.Off;
            foreach (float side in new[] { -1f, 1f })
            {
                var fin = Bit("Fin", shell, cube, finMaterial, new Vector3(side * 0.19f, 0.01f, -0.03f), new Vector3(0.025f, 0.13f, 0.17f), new Vector3(0f, side * 12f, side * -8f));
                Bit("Strip", fin.transform, cube, irisMaterial, new Vector3(side * 0.6f, 0f, 0f), new Vector3(0.3f, 0.7f, 0.12f)).shadowCastingMode = ShadowCastingMode.Off;
            }
            var glow = Bit("Glow", root.transform, billboard, glowMaterial, Vector3.zero, Vector3.one * 0.95f);
            glow.shadowCastingMode = ShadowCastingMode.Off;
            var shine = new GameObject("Shine").AddComponent<Light>();
            shine.transform.SetParent(root.transform, false);
            shine.type = LightType.Point;
            shine.color = new Color(1f, 0.66f, 0.36f);
            shine.range = 7f;
            shine.intensity = 1.6f;
            shine.shadows = LightShadows.Soft;
            shine.shadowStrength = 0.6f;
            var beam = new GameObject("Beam") { layer = layer }.AddComponent<LineRenderer>();
            beam.transform.SetParent(root.transform, false);
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.sharedMaterial = beamMaterial;
            beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, 0.16f));
            beam.numCapVertices = 4;
            beam.alignment = LineAlignment.View;
            beam.shadowCastingMode = ShadowCastingMode.Off;
            // The eye's own camera, just in front of the iris, blind to the shell.
            var lensObject = new GameObject("Eye Camera") { layer = layer };
            lensObject.transform.SetParent(irisPivot, false);
            lensObject.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            var lens = lensObject.AddComponent<Camera>();
            lens.enabled = false;
            lens.fieldOfView = 60f;
            lens.nearClipPlane = 0.1f;
            lens.farClipPlane = 3000f;
            lens.cullingMask = ~(1 << layer);
            lens.clearFlags = CameraClearFlags.Skybox;
            lens.allowHDR = true;
            var lensData = lens.GetUniversalAdditionalCameraData();
            lensData.renderPostProcessing = true;
            lensData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            lensData.renderShadows = true;
            var eye = root.AddComponent<Eye>();
            Wire(eye, ("lens", lens));
            var hum = MakeSource("Hum", root.transform, FindClip("companion_hum_loop"), true);
            hum.spatialBlend = 0.6f;
            hum.playOnAwake = true;
            hum.volume = 0.2f;
            var voice = MakeSource("Voice", root.transform, null, false);
            voice.spatialBlend = 0.35f;
            var drone = root.AddComponent<Drone>();
            Wire(drone, ("travellerBody", travellerBody), ("follow", follow), ("shell", shell), ("iris", irisPivot), ("irisRenderer", irisRenderer),
                ("glow", glow), ("shine", shine), ("beam", beam), ("eye", eye), ("hum", hum), ("voice", voice), ("moveClip", FindClip("drone_move")));
            return drone;
        }

        /// <summary>
        /// The vault, dug into the hill behind where the survivor wakes: a concrete front, a round door
        /// on a hinge, two small windows still warm inside, a light that wakes at the end.
        /// </summary>
        struct Station
        {
            public Gate Gate;
            public Collider Inside;
            public Frozen Case;
            public Holdable Heart;
        }

        /// <summary>
        /// The old station: a square of dark stone walls ten metres a side, no roof. Its door, facing
        /// the chasm, sinks into the ground while enough weight rests on the plate before it. Inside,
        /// the last heart, frozen in a block of ice, and writing scratched on the north wall.
        /// </summary>
        static Station MakeStation(Transform parent, Vector3 centre, Mesh cube, Mesh cylinder, Mesh quad, Mesh sphere, Material wallMaterial, Material plateMaterial,
            Material lightMaterial, Material iceMaterial, Material writingMaterial, Material steamMaterial, List<Landmark> landmarks, Transform traveller)
        {
            const float side = 10f, height = 4.5f, thick = 0.6f, door = 2.4f;
            var station = new Station();
            var root = new GameObject("Station").transform;
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
            Wall("Floor", new Vector3(0f, -0.35f, 0f), new Vector3(side, 0.8f, side));
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
            var grind = MakeSource("Grinding", root, null, false);
            grind.spatialBlend = 0.8f;
            station.Gate = root.gameObject.AddComponent<Gate>();
            Wire(station.Gate, ("door", doorPart.transform), ("plate", plate.transform), ("pressing", pressing), ("traveller", traveller), ("sound", grind),
                ("moveClip", FindClip("build_apart_2") ?? FindClip("build_lock_stone_2")));

            // Inside: everywhere within the walls.
            var inside = new GameObject("Inside").AddComponent<BoxCollider>();
            inside.transform.SetParent(root, false);
            inside.isTrigger = true;
            inside.center = new Vector3(0f, 1.5f, 0.3f);
            inside.size = new Vector3(side - 1.4f, 3f, side - 1.8f);
            inside.gameObject.layer = 2;
            station.Inside = inside;

            // The writing on the north wall, inside.
            var message = MakePart("Writing", root, quad, writingMaterial, 1f);
            message.transform.localPosition = new Vector3(-0.6f, 2.15f, side * 0.5f - thick * 0.5f - 0.02f);
            message.transform.localScale = new Vector3(3.6f, 2.25f, 1f);
            message.shadowCastingMode = ShadowCastingMode.Off;
            landmarks.Add(MakeLandmark("Writing Place", message.transform, message.transform.position, "MESSAGE", "the writing on the wall",
                "Mara Okonjo's last words, scratched into the station's inner north wall in her hand, too small to read from the door. She came here for the heart in year 98 and never came home", new Vector3(3.6f, 2.2f, 0.3f)));

            // The heart: a barrel of dark metal banded with light, frozen in a block of ice.
            var heartObject = new GameObject("Heart");
            heartObject.transform.SetParent(root, false);
            heartObject.transform.localPosition = new Vector3(1.2f, 0.05f, 0.8f);
            var heartBody = MakePart("Cell", heartObject.transform, cylinder, wallMaterial, 1f);
            heartBody.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            heartBody.transform.localScale = new Vector3(0.46f, 0.45f, 0.46f);
            heartBody.shadowCastingMode = ShadowCastingMode.On;
            heartBody.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            foreach (float band in new[] { 0.2f, 0.62f, 0.84f })
            {
                var glowBand = MakePart("Band", heartObject.transform, cylinder, lightMaterial, 1f);
                glowBand.transform.localPosition = new Vector3(0f, band, 0f);
                glowBand.transform.localScale = new Vector3(0.48f, 0.025f, 0.48f);
            }
            var heartLight = new GameObject("Heart Light").AddComponent<Light>();
            heartLight.transform.SetParent(heartObject.transform, false);
            heartLight.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            heartLight.type = LightType.Point;
            heartLight.color = new Color(1f, 0.72f, 0.4f);
            heartLight.range = 4f;
            heartLight.intensity = 1.2f;
            var heartBodyRb = heartObject.AddComponent<Rigidbody>();
            heartBodyRb.mass = 40f;
            heartBodyRb.isKinematic = true;
            heartBodyRb.interpolation = RigidbodyInterpolation.Interpolate;
            heartObject.AddComponent<Heart>();
            var heart = heartObject.AddComponent<Holdable>();
            heart.Title = "the heart";
            heart.Mass = 40f;
            heart.Grip = new Vector3(0f, 0.45f, 0f);
            station.Heart = heart;
            landmarks.Add(MakeLandmark("Heart Place", heartObject.transform, heartObject.transform.position + Vector3.up * 0.35f, "HEART", "the heart",
                "the last spare heart for the mirror: a power cell like a small barrel, 40 kg, banded with dim light", new Vector3(0.6f, 0.75f, 0.6f)));

            var caseObject = new GameObject("Ice Case");
            caseObject.transform.SetParent(root, false);
            caseObject.transform.localPosition = heartObject.transform.localPosition;
            var shell = MakePart("Ice", caseObject.transform, cube, iceMaterial, 1f);
            shell.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            shell.transform.localScale = new Vector3(1.05f, 0.72f, 1.05f);
            shell.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
            shell.shadowCastingMode = ShadowCastingMode.On;
            shell.gameObject.AddComponent<BoxCollider>();
            var frozen = caseObject.AddComponent<Frozen>();
            var (drips, steam, crack) = IceEffects(caseObject.transform, new Vector3(0f, 0.6f, 0f), 0.6f, steamMaterial);
            Wire(frozen, ("shell", shell), ("drips", drips), ("steam", steam), ("sound", crack),
                ("crackClip", FindClip("ice_crack")), ("breakClip", FindClip("ice_break")), ("meltClip", FindClip("ice_melt_loop")));
            SetValue(frozen, "needs", 12f);
            WireArray(frozen, "holds", new Object[] { heart });
            station.Case = frozen;
            landmarks.Add(MakeLandmark("Case Place", caseObject.transform, caseObject.transform.position + Vector3.up * 0.6f, "ICE_CASE", "the case of ice",
                "a block of clear ice frozen round the heart; heat melts it and a pick breaks it", new Vector3(1.2f, 1.15f, 1.2f)));

            landmarks.Add(MakeLandmark("Station Place", root, root.position + Vector3.up * 2f, "STATION", "the old station",
                $"a square building of dark stone {side:F0} m across, walls {height} m high, no roof; its one door faces the chasm", new Vector3(side + 1.5f, height + 1f, side + 1.5f)));
            landmarks.Add(MakeLandmark("Door", doorPart.transform, doorPart.transform.position, "DOOR", "the station door",
                "a stone door; it sinks into the ground while something heavy (about 250 kg, more than three people) rests on the plate before it", new Vector3(1f, 1f, 1.6f)));
            landmarks.Add(MakeLandmark("Plate Place", plateRoot, plateRoot.position, "PLATE", "the plate",
                "a square stone plate in the ground before the door; the survivor alone is too light to press it down", new Vector3(2f, 0.6f, 2f)));
            return station;
        }

        /// <summary>Drips and steam for ice as it melts, and a voice for it to crack and break with.</summary>
        static (ParticleSystem, ParticleSystem, AudioSource) IceEffects(Transform parent, Vector3 at, float size, Material steamMaterial)
        {
            var drips = MakeParticles("Drips", parent, steamMaterial);
            drips.transform.localPosition = at;
            var main = drips.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(0.7f, 0.85f, 1f, 0.9f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = drips.emission;
            emission.rateOverTime = 0f;
            var shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one * size * 1.8f;
            var steam = MakeParticles("Steam", parent, steamMaterial);
            steam.transform.localPosition = at + Vector3.up * size;
            main = steam.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startColor = new Color(0.9f, 0.9f, 0.95f, 0.25f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            emission = steam.emission;
            emission.rateOverTime = 0f;
            shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one * size * 1.4f;
            var fade = steam.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var crack = MakeSource("Ice Voice", parent, null, false);
            crack.spatialBlend = 0.9f;
            crack.minDistance = 3f;
            crack.maxDistance = 40f;
            return (drips, steam, crack);
        }

        struct MirrorParts
        {
            public Mirror Mirror;
            public Frozen Joint;
            public Socket Socket;
        }

        /// <summary>
        /// The old sun mirror on the clifftop: a short tower, a joint crusted in ice, a great dish of
        /// dark metal facing nowhere, an empty socket at the tower's foot, and the beam it will throw.
        /// </summary>
        static MirrorParts MakeMirror(Transform parent, Vector3 at, Mesh cube, Mesh cylinder, Mesh sphere, Material metal, Material ice, Material light,
            Material shaftMaterial, Material steamMaterial, Transform vault, List<Landmark> landmarks, Transform traveller)
        {
            var parts = new MirrorParts();
            var root = new GameObject("Sun Mirror").transform;
            root.SetParent(parent, false);
            root.position = at;
            var tower = MakePart("Tower", root, cylinder, metal, 1f);
            tower.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            tower.transform.localScale = new Vector3(0.9f, 1.4f, 0.9f);
            tower.shadowCastingMode = ShadowCastingMode.On;
            tower.gameObject.AddComponent<CapsuleCollider>();
            var footing = MakePart("Footing", root, cylinder, metal, 1f);
            footing.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            footing.transform.localScale = new Vector3(2.2f, 0.15f, 2.2f);
            footing.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.75f, 2f, 0.75f);
            // The joint, and the dish that turns on it.
            var jointPivot = new GameObject("Joint").transform;
            jointPivot.SetParent(root, false);
            jointPivot.localPosition = new Vector3(0f, 3f, 0f);
            var knuckle = MakePart("Knuckle", jointPivot, sphere, metal, 0.75f);
            knuckle.shadowCastingMode = ShadowCastingMode.On;
            var dish = new GameObject("Dish").transform;
            dish.SetParent(jointPivot, false);
            // Facing away from the vault and down a little: dead.
            dish.rotation = Quaternion.LookRotation(new Vector3(0.3f, -0.5f, 1f).normalized, Vector3.up);
            var arm = MakePart("Arm", dish, cube, metal, 1f);
            arm.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            arm.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
            var face = MakePart("Face", dish, sphere, metal, 1f);
            face.transform.localPosition = new Vector3(0f, 0f, 1.1f);
            face.transform.localScale = new Vector3(4.6f, 4.6f, 0.4f);
            face.shadowCastingMode = ShadowCastingMode.On;
            face.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.9f, 0.5f);
            var rim = MakePart("Rim", dish, MakeRing("Mirror Rim", 96, 0.93f, 1f), light, 1f);
            rim.transform.localPosition = new Vector3(0f, 0f, 1.25f);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localScale = Vector3.one * 4.65f;
            rim.shadowCastingMode = ShadowCastingMode.Off;
            var focus = new GameObject("Focus").transform;
            focus.SetParent(dish, false);
            focus.localPosition = new Vector3(0f, 0f, 3.2f);
            // The ice on the joint.
            var jointIce = new GameObject("Joint Ice");
            jointIce.transform.SetParent(root, false);
            jointIce.transform.localPosition = new Vector3(0f, 3f, 0f);
            var crust = MakePart("Crust", jointIce.transform, sphere, ice, 1.35f);
            crust.transform.localScale = new Vector3(1.5f, 1.3f, 1.5f);
            crust.shadowCastingMode = ShadowCastingMode.On;
            crust.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            var frozen = jointIce.AddComponent<Frozen>();
            var (drips, steam, crack) = IceEffects(jointIce.transform, Vector3.zero, 0.7f, steamMaterial);
            Wire(frozen, ("shell", crust), ("drips", drips), ("steam", steam), ("sound", crack),
                ("crackClip", FindClip("ice_crack")), ("breakClip", FindClip("ice_break")), ("meltClip", FindClip("ice_melt_loop")));
            SetValue(frozen, "needs", 12f);
            parts.Joint = frozen;
            // The socket at the tower's foot, facing the way up from the cliff's edge.
            var socketRoot = new GameObject("Socket").transform;
            socketRoot.SetParent(root, false);
            socketRoot.localPosition = new Vector3(0f, 0f, -1.4f);
            var housing = MakePart("Housing", socketRoot, cube, metal, 1f);
            housing.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            housing.transform.localScale = new Vector3(1f, 0.7f, 0.9f);
            housing.shadowCastingMode = ShadowCastingMode.On;
            housing.gameObject.AddComponent<BoxCollider>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(socketRoot, false);
            seat.localPosition = new Vector3(0f, 0.7f, 0f);
            var socketGlow = MakePart("Socket Glow", socketRoot, cylinder, light, 1f);
            socketGlow.transform.localPosition = new Vector3(0f, 0.71f, 0f);
            socketGlow.transform.localScale = new Vector3(0.6f, 0.01f, 0.6f);
            socketGlow.enabled = false;
            var socket = socketRoot.gameObject.AddComponent<Socket>();
            socket.Set(seat, socketGlow);
            parts.Socket = socket;
            // The beam it will throw, and the light it will give.
            var shaft = new GameObject("Beam").AddComponent<LineRenderer>();
            shaft.transform.SetParent(root, false);
            shaft.useWorldSpace = true;
            shaft.positionCount = 2;
            shaft.sharedMaterial = shaftMaterial;
            shaft.alignment = LineAlignment.View;
            shaft.numCapVertices = 6;
            shaft.shadowCastingMode = ShadowCastingMode.Off;
            var shine = new GameObject("Mirror Light").AddComponent<Light>();
            shine.transform.SetParent(focus, false);
            shine.type = LightType.Point;
            shine.color = new Color(1f, 0.75f, 0.45f);
            shine.range = 30f;
            shine.intensity = 0f;
            var hum = MakeSource("Mirror Sound", root, null, false);
            hum.spatialBlend = 0.6f;
            hum.minDistance = 6f;
            hum.maxDistance = 120f;
            parts.Mirror = root.gameObject.AddComponent<Mirror>();
            Wire(parts.Mirror, ("dish", dish), ("face", face), ("focus", focus), ("beam", shaft), ("vault", vault),
                ("shine", shine), ("sound", hum), ("wakeClip", FindClip("mirror_wake")), ("turnClip", FindClip("mirror_turn")));
            SetValue(parts.Mirror, "sunDirection", SunDirection);
            landmarks.Add(MakeLandmark("Mirror Place", root, root.position + Vector3.up * 2f, "MIRROR", "the sun mirror",
                "the old sun mirror: a great dish on a short tower; turned to the sun, it threw light onto the vault and kept the valley warm", new Vector3(5f, 6f, 5f)));
            landmarks.Add(MakeLandmark("Joint Place", jointIce.transform, jointIce.transform.position, "MIRROR_JOINT", "the mirror's joint",
                "the joint the dish turns on, 3 m up the tower, crusted in thick ice; heat melts it, a pick breaks it", new Vector3(1.6f, 1.4f, 1.6f)));
            landmarks.Add(MakeLandmark("Socket Place", socketRoot, socketRoot.position + Vector3.up * 0.5f, "SOCKET", "the mirror's socket",
                "the socket for the mirror's heart, at the foot of its tower", new Vector3(1.1f, 0.9f, 1f)));
            return parts;
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

        static PhysicsMaterial MakePhysics(string name, float friction, float bounce = 0f)
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
            material.bounciness = bounce;
            material.bounceCombine = bounce > 0f ? PhysicsMaterialCombine.Maximum : PhysicsMaterialCombine.Average;
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
            grading.postExposure.Override(1.15f);
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
