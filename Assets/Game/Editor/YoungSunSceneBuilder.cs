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
    /// Builds the young solar system (phase 1) from nothing, like the opening: the sky and its stars,
    /// the young Sun, the molten Earth with its air, Theia on Earth's path, the other planets, the
    /// dust and the icy belt, the effects of every act, the player, the notes, the voice, the oracle
    /// and the director, all wired together. Running it again replaces the scene.
    /// </summary>
    public static class YoungSunSceneBuilder
    {
        const string ScenePath = Root + "/Scenes/YoungSun.unity";
        const float EarthRadius = 10f;
        const float SunDistance = 3000f;
        const float SunRadius = 40f;
        const float StarDistance = 7000f;
        static readonly Vector3 SunDirection = new Vector3(0.12f, 0.06f, 1f).normalized;

        [MenuItem("Game/Build Young Sun Scene")]
        public static void Build()
        {
            foreach (string folder in new[] { "Scenes", "Materials", "Textures", "Meshes", "Settings", "UI", "Story" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{folder}")) AssetDatabase.CreateFolder(Root, folder);

            var softDot = MakeTexture("Soft Dot", 64, true, SoftDot);
            var digits = MakeTexture("Digits", 256, true, Digits);
            var paper = MakeTexture("Paper", 1024, false, Paper);
            var sphere = MakeSphere(160, 96);
            var billboard = MakeBillboard();
            var stars = MakeStars(6000, 7);

            var sky = MakeMaterial("Space Sky", "Game/SpaceSky", m => m.SetVector("_SunDirection", SunDirection));
            var starMaterial = MakeMaterial("Stars", "Game/Stars", m =>
            {
                m.SetFloat("_Distance", StarDistance);
                m.SetFloat("_Intensity", 1.6f);
                m.SetFloat("_Twinkle", 0.18f);
            });
            var earthMaterial = MakePlanet("Earth", new Vector3(1.3f, 4.2f, 2.7f), new Color(0.09f, 0.085f, 0.085f), new Color(0.26f, 0.23f, 0.21f), 0.3f, 2.6f, 0f, m =>
            {
                m.SetColor("_Lava", new Color(0.85f, 0.2f, 0.03f));
                m.SetColor("_LavaHot", new Color(1f, 0.55f, 0.14f));
            });
            var airMaterial = MakeMaterial("Earth Air", "Game/Atmosphere", m =>
            {
                m.SetColor("_Haze", new Color(1f, 0.45f, 0.22f));
                m.SetFloat("_Thickness", 1.1f);
                m.SetFloat("_Planet", 1f / 1.09f);
                m.SetFloat("_Aurora", 0f);
            });
            var theiaMaterial = MakePlanet("Theia", new Vector3(7.1f, 2.2f, 9.4f), new Color(0.2f, 0.1f, 0.07f), new Color(0.48f, 0.28f, 0.18f), 0f, 0.6f, 0.7f);
            var moonMaterial = MakePlanet("Moon", new Vector3(3.3f, 8.8f, 1.1f), new Color(0.17f, 0.17f, 0.18f), new Color(0.45f, 0.45f, 0.46f), 0.7f, 1.8f, 0.8f, m =>
            {
                m.SetColor("_Lava", new Color(0.85f, 0.2f, 0.03f));
                m.SetColor("_LavaHot", new Color(1f, 0.55f, 0.14f));
            });
            var marsMaterial = MakePlanet("Mars", new Vector3(5.5f, 1.9f, 6.6f), new Color(0.33f, 0.13f, 0.07f), new Color(0.6f, 0.32f, 0.18f), 0f, 0f, 0.4f);
            var venusMaterial = MakePlanet("Venus", new Vector3(2.4f, 6.1f, 3.9f), new Color(0.5f, 0.45f, 0.35f), new Color(0.7f, 0.62f, 0.45f), 0f, 0f, 0f, m =>
            {
                m.SetFloat("_Clouds", 1f);
                m.SetColor("_CloudColor", new Color(0.95f, 0.87f, 0.64f));
            });
            var jupiterMaterial = MakeMaterial("Jupiter", "Game/GasGiant", m =>
            {
                m.SetColor("_Light", new Color(0.88f, 0.8f, 0.66f));
                m.SetColor("_Dark", new Color(0.55f, 0.38f, 0.25f));
                m.SetFloat("_Bands", 16f);
            });
            var saturnMaterial = MakeMaterial("Saturn", "Game/GasGiant", m =>
            {
                m.SetColor("_Light", new Color(0.9f, 0.84f, 0.62f));
                m.SetColor("_Dark", new Color(0.7f, 0.6f, 0.42f));
                m.SetFloat("_Bands", 22f);
                m.SetVector("_Seed", new Vector4(4f, 1f, 2f, 0f));
            });
            var sunMaterial = MakeMaterial("Sun", "Game/Sun", m => m.SetFloat("_Intensity", 7f));
            var coronaMaterial = MakeMaterial("Sun Corona", "Game/Corona", m =>
            {
                m.SetColor("_Inner", new Color(1f, 0.93f, 0.78f));
                m.SetColor("_Outer", new Color(1f, 0.55f, 0.26f));
                m.SetFloat("_Edge", 1f / 14f);
                m.SetFloat("_Intensity", 1.1f);
            });
            var sunGlowMaterial = MakeMaterial("Sun Glow", "Game/Halo", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.82f, 0.6f));
                m.SetFloat("_Edge", 1f / 9f);
                m.SetFloat("_Intensity", 0.9f);
            });
            var glyphMaterial = MakeMaterial("Sky Answer", "Game/Glyph", m => m.SetFloat("_Intensity", 4f));
            var streakMaterial = MakeMaterial("Shooting Star", "Game/Streak", m => m.SetFloat("_Intensity", 6f));
            var emberMaterial = MakeParticleMaterial("Ember", softDot, 2f);
            var iceMaterial = MakeParticleMaterial("Ice Speck", softDot, 1.4f);
            var dustMaterial = MakeParticleMaterial("Space Dust", softDot, 0.5f);
            var windMaterial = MakeParticleMaterial("Solar Wind", softDot, 0.9f);
            var digitMaterial = MakeParticleMaterial("Digit", digits, 3f);

            var profile = MakeYoungSunProfile();
            var panel = MakePanel();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.012f, 0.012f, 0.018f);
            RenderSettings.fog = false;

            // The Sun, far off: its light is the scene's main light.
            var sunObject = new GameObject("Sun");
            sunObject.transform.position = SunDirection * SunDistance;
            var sunBody = MakePart("Body", sunObject.transform, sphere, sunMaterial, 2f * SunRadius);
            MakePart("Corona", sunObject.transform, billboard, coronaMaterial, 2f * SunRadius * 14f);
            MakePart("Glow", sunObject.transform, billboard, sunGlowMaterial, 2f * SunRadius * 9f);
            var sunlight = new GameObject("Sunlight").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sunlight.color = new Color(1f, 0.95f, 0.88f);
            sunlight.intensity = 1.8f;
            sunlight.shadows = LightShadows.None;
            RenderSettings.sun = sunlight;

            // Earth, molten under a dark crust, with its air.
            var earthObject = new GameObject("Earth");
            earthObject.transform.localScale = Vector3.one * (2f * EarthRadius);
            earthObject.AddComponent<MeshFilter>().sharedMesh = sphere;
            var earthRenderer = earthObject.AddComponent<MeshRenderer>();
            earthRenderer.sharedMaterial = earthMaterial;
            earthRenderer.shadowCastingMode = ShadowCastingMode.Off;
            earthObject.AddComponent<SphereCollider>().radius = 0.5f;
            var air = MakePart("Air", earthObject.transform, sphere, airMaterial, 1.09f);
            var earth = earthObject.AddComponent<Planet>();
            Wire(earth, ("surface", earthRenderer), ("air", air));
            SetValue(earth, "spin", 0.6f);

            // Theia, on Earth's path.
            var theia = MakeBody("Theia", new Vector3(150f, 25f, 60f), EarthRadius * 0.53f, sphere, theiaMaterial, 0.3f);
            // The Moon, not there yet.
            var moon = MakeBody("Moon", new Vector3(40f, 0f, 0f), EarthRadius * 0.27f, sphere, moonMaterial, 0f);
            var moonOrbit = moon.gameObject.AddComponent<Orbit>();
            Wire(moonOrbit, ("centre", earthObject.transform));
            SetValue(moonOrbit, "radius", 40f);
            SetValue(moonOrbit, "period", 100f);

            // The other planets, much nearer than they are, so they can be seen.
            MakeBody("Venus", new Vector3(-700f, 20f, 900f), 9.5f, sphere, venusMaterial, 0.05f);
            MakeBody("Mars", new Vector3(-600f, 30f, -1100f), 5.3f, sphere, marsMaterial, 0.4f);
            MakePart("Jupiter", null, sphere, jupiterMaterial, 80f).transform.position = new Vector3(2500f, 120f, -5200f);
            MakePart("Saturn", null, sphere, saturnMaterial, 64f).transform.position = new Vector3(-4200f, 200f, -4800f);

            // The stars, kept far around the player.
            var starsObject = new GameObject("Stars");
            starsObject.AddComponent<MeshFilter>().sharedMesh = stars;
            var starRenderer = starsObject.AddComponent<MeshRenderer>();
            starRenderer.sharedMaterial = starMaterial;
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Dust in the plane of the planets, and the icy belt far out, away from the Sun.
            var dust = MakeParticles("Dust", null, dustMaterial);
            Field(dust, 2600, new Vector2(0.6f, 2.4f), new Color(0.9f, 0.8f, 0.65f, 0.45f));
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Donut;
            dustShape.radius = 1600f;
            dustShape.donutRadius = 1000f;
            dustShape.rotation = new Vector3(90f, 0f, 0f);
            dustShape.scale = new Vector3(1f, 1f, 0.05f);
            var iceBelt = MakeParticles("Ice Belt", null, iceMaterial);
            iceBelt.transform.position = new Vector3(900f, 60f, -2400f);
            Field(iceBelt, 1800, new Vector2(2.5f, 7f), new Color(0.72f, 0.86f, 1f, 0.8f));
            var beltShape = iceBelt.shape;
            beltShape.shapeType = ParticleSystemShapeType.Box;
            beltShape.scale = new Vector3(1600f, 120f, 400f);

            // What the acts need.
            var ejecta = MakeParticles("Ejecta", null, emberMaterial);
            Ejecta(ejecta);
            var debrisRing = MakeParticles("Debris Ring", null, emberMaterial);
            DebrisRing(debrisRing);
            var iceStream = MakeParticles("Ice Stream", null, iceMaterial);
            iceStream.transform.position = earthObject.transform.position + (iceBelt.transform.position - earthObject.transform.position).normalized * 360f;
            iceStream.transform.rotation = Quaternion.LookRotation(earthObject.transform.position - iceStream.transform.position);
            IceStream(iceStream, iceMaterial);
            var volcanoes = MakeParticles("Volcanoes", earthObject.transform, emberMaterial);
            Volcanoes(volcanoes);
            var wind = MakeParticles("Solar Wind", null, windMaterial);
            wind.transform.position = earthObject.transform.position + SunDirection * 150f;
            wind.transform.rotation = Quaternion.LookRotation(-SunDirection);
            Wind(wind);
            var shield = new GameObject("Shield").AddComponent<ParticleSystemForceField>();
            shield.transform.position = earthObject.transform.position;
            shield.shape = ParticleSystemForceFieldShape.Sphere;
            shield.endRange = 42f;
            shield.gravity = 0f;
            shield.gravityFocus = 0f;

            // The player, a little past Earth's terminator: a lit crescent for seas and clouds, a night
            // side for the glowing cracks and the auroras; the Sun out of view to the right.
            var playerObject = new GameObject("Player");
            Vector3 aside = Vector3.Cross(Vector3.up, SunDirection).normalized;
            playerObject.transform.position = (aside * 0.95f - SunDirection * 0.3f + Vector3.up * 0.12f).normalized * 70f;
            Vector3 toEarth = earthObject.transform.position - playerObject.transform.position;
            playerObject.transform.rotation = Quaternion.LookRotation(new Vector3(toEarth.x, 0f, toEarth.z));
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localRotation = Quaternion.Euler(-Mathf.Asin(toEarth.normalized.y) * Mathf.Rad2Deg, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 8000f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderShadows = false;
            cameraData.dithering = true;
            cameraData.stopNaN = true;
            cameraObject.AddComponent<AudioListener>();
            var player = playerObject.AddComponent<FloatingPlayer>();
            Wire(player, ("view", camera), ("orb", earthObject.transform));
            SetValue(player, "nearest", 24f);
            SetValue(player, "farthest", 420f);
            SetValue(player, "thrust", 9f);
            SetValue(player, "maxSpeed", 18f);
            SetValue(player, "damping", 0.9f);

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            // Sound.
            var sound = new GameObject("Sound").transform;
            var ambience = MakeSource("Ambience", sound, FindClip("young_sun_ambience"), true);
            var music = MakeSource("Music", sound, FindClip("music_void"), true);
            var effects = MakeSource("Effects", sound, null, false);
            var skySound = MakeSource("Sky", sound, null, false);
            var talkSound = MakeSource("Talk", sound, null, false);
            var paperSound = MakeSource("Paper", sound, null, false);

            // What is drawn over the world: the HUD and the notes share the screen's panel.
            var notepadStyle = Load<StyleSheet>($"{Root}/UI/Notepad.uss");
            var hudStyle = Load<StyleSheet>($"{Root}/UI/Hud.uss");
            var interfaceObject = new GameObject("Interface");
            var document = interfaceObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var hud = interfaceObject.AddComponent<Hud>();
            Wire(hud, ("document", document), ("style", hudStyle));
            var journal = interfaceObject.AddComponent<Journal>();
            Wire(journal, ("document", document), ("style", notepadStyle), ("paper", paper), ("player", player), ("sound", paperSound),
                ("openClip", FindClip("notebook_raise") ?? FindClip("paper_open")), ("closeClip", FindClip("notebook_lower") ?? FindClip("paper_close")),
                ("writeClip", FindClip("pencil_write")));
            WireArray(journal, "strokeClips", Enumerable.Range(1, 6).Select(i => (Object)FindClip($"pencil_stroke_{i}")).Where(c => c != null).ToArray());

            // The entity's voice: answers in the stars, shooting stars, and the stream that points.
            var skyObject = new GameObject("Sky Answer");
            var figure = new GameObject("Figure");
            figure.transform.SetParent(skyObject.transform, false);
            figure.AddComponent<MeshFilter>();
            var figureRenderer = figure.AddComponent<MeshRenderer>();
            figureRenderer.sharedMaterial = glyphMaterial;
            figureRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var meteor = new GameObject("Shooting Star").AddComponent<LineRenderer>();
            meteor.transform.SetParent(skyObject.transform, false);
            meteor.sharedMaterial = streakMaterial;
            meteor.useWorldSpace = true;
            meteor.positionCount = 16;
            meteor.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 1f));
            meteor.widthMultiplier = 16f;
            meteor.textureMode = LineTextureMode.Stretch;
            meteor.alignment = LineAlignment.View;
            meteor.numCapVertices = 2;
            meteor.shadowCastingMode = ShadowCastingMode.Off;
            var skyAnswer = skyObject.AddComponent<SkyAnswer>();
            Wire(skyAnswer, ("view", camera), ("figure", figureRenderer), ("meteor", meteor), ("sound", skySound),
                ("yesClip", FindClip("answer_yes")), ("noClip", FindClip("answer_no")), ("meteorClip", FindClip("shooting_star")));
            SetStructs(skyAnswer, "blockers", new (Object body, float margin)[]
            {
                (earthObject.transform, 3f), (theia.transform, 1f), (moon.transform, 1f), (sunBody.transform, 12f),
            }, (p, item) =>
            {
                p.FindPropertyRelative("body").objectReferenceValue = item.body;
                p.FindPropertyRelative("margin").floatValue = item.margin;
            });

            var streamObject = MakeParticles("Pointing Stream", null, digitMaterial);
            DigitStream(streamObject);
            var stream = streamObject.gameObject.AddComponent<PointingStream>();
            Wire(stream, ("digits", streamObject), ("view", camera), ("sound", skySound), ("clip", FindClip("stream")));

            // Speaking, and the oracle that reads what was said.
            var talk = interfaceObject.AddComponent<PushToTalk>();
            Wire(talk, ("hud", hud), ("sound", talkSound), ("startClip", FindClip("listen_on")), ("stopClip", FindClip("listen_off")));
            var brainObject = new GameObject("Oracle");
            var oracle = brainObject.AddComponent<Oracle>();
            Wire(oracle, ("card", Load<TextAsset>($"{Root}/Story/Young Sun card.txt")));
            SetValue(oracle, "timeout", 15f);
            SetString(oracle, "effort", "minimal");
            SetStructs(oracle, "actions", Actions, (p, item) =>
            {
                p.FindPropertyRelative("id").stringValue = item.id;
                var words = p.FindPropertyRelative("words");
                words.arraySize = item.words.Length;
                for (int i = 0; i < item.words.Length; i++) words.GetArrayElementAtIndex(i).stringValue = item.words[i];
            });
            SetStructs(oracle, "targets", new[] { "THEIA", "ICE_BELT", "VOLCANOES", "CORE", "SUN", "EARTH" }, (p, item) => p.stringValue = item);

            // The world and the director.
            var worldObject = new GameObject("World");
            var world = worldObject.AddComponent<YoungWorld>();
            Wire(world, ("earth", earth), ("theia", theia), ("moon", moon), ("moonOrbit", moonOrbit), ("sun", sunObject.transform), ("sunlight", sunlight),
                ("viewer", camera.transform), ("player", player), ("hud", hud), ("ejecta", ejecta), ("debrisRing", debrisRing), ("iceStream", iceStream),
                ("volcanoes", volcanoes), ("shield", shield), ("effects", effects),
                ("approachClip", FindClip("impact_approach")), ("boomClip", FindClip("impact_boom")), ("moonClip", FindClip("moon_form")),
                ("timeClip", FindClip("time_pass")), ("iceClip", FindClip("ice_rain")), ("volcanoClip", FindClip("volcano")),
                ("auroraClip", FindClip("aurora")), ("rewindClip", FindClip("rewind")), ("orbitClip", FindClip("orbit_move")));
            var director = new GameObject("Director").AddComponent<YoungSunDirector>();
            Wire(director, ("player", player), ("world", world), ("oracle", oracle), ("sky", skyAnswer), ("stream", stream), ("journal", journal),
                ("talk", talk), ("hud", hud), ("ambience", ambience), ("music", music),
                ("earth", earthObject.transform), ("theia", theia.transform), ("iceBelt", iceBelt.transform), ("sun", sunBody.transform));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int opening = scenes.FindIndex(s => s.path.EndsWith("/Opening.unity"));
            scenes.Insert(opening + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}");
        }

        /// <summary>Every action the card names, with words the offline rules look for.</summary>
        static readonly (string id, string[] words)[] Actions =
        {
            ("THEIA_HEADON", new[] { "head on", "straight into", "right into" }),
            ("THEIA_GLANCE", new[] { "theia", "red planet", "red world", "that planet", "crash", "collide", "smash", "hit" }),
            ("PASS_TIME", new[] { "wait", "time", "cool", "crust", "harden", "million" }),
            ("SEND_COMETS", new[] { "comet" }),
            ("SEND_ASTEROIDS", new[] { "asteroid", "icy", "ice", "water", "rain", "ocean", "sea" }),
            ("OUTGAS", new[] { "volcano", "greenhouse", "carbon", "co2", "warm", "thick", "heat" }),
            ("MOVE_CLOSER", new[] { "closer", "nearer" }),
            ("MOVE_FARTHER", new[] { "farther", "further", "away from the sun" }),
            ("STIR_CORE", new[] { "magnet", "core", "shield", "protect", "dynamo", "aurora" }),
            ("MAKE", new[] { "make a moon", "create" }),
            ("LATER", new[] { "life", "people", "humans", "animals", "plants" }),
            ("BREAK", new[] { "destroy", "explode", "blow up" }),
            ("OTHER", new string[0]),
        };

        // ---- Bodies and effects ----

        static Planet MakeBody(string name, Vector3 position, float radius, Mesh sphere, Material material, float spin)
        {
            var body = MakePart(name, null, sphere, material, 2f * radius);
            body.transform.position = position;
            var planet = body.gameObject.AddComponent<Planet>();
            Wire(planet, ("surface", body));
            SetValue(planet, "spin", spin);
            return planet;
        }

        static Material MakePlanet(string name, Vector3 seed, Color rock, Color high, float molten, float glow, float craters, Action<Material> more = null) =>
            MakeMaterial(name, "Game/Planet", m =>
            {
                m.SetVector("_Seed", seed);
                m.SetColor("_Rock", rock);
                m.SetColor("_RockHigh", high);
                m.SetFloat("_Molten", molten);
                m.SetFloat("_Glow", glow);
                m.SetFloat("_Craters", craters);
                m.SetFloat("_Sea", 0f);
                m.SetFloat("_Steam", 0f);
                m.SetFloat("_Clouds", 0f);
                m.SetFloat("_Ice", 0f);
                more?.Invoke(m);
            });

        /// <summary>A still field of specks, all there from the start and never dying.</summary>
        static void Field(ParticleSystem particles, int count, Vector2 size, Color colour)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1e6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = colour;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        static void Ejecta(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 2.6f);
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1000) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 2.5f;
            var limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 4f;
            limit.dampen = 0.06f;
            Fade(particles, new Color(1f, 0.95f, 0.8f), new Color(1f, 0.45f, 0.12f), new Color(0.5f, 0.1f, 0.04f));
        }

        static void DebrisRing(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 260f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 38f;
            shape.donutRadius = 13f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.scale = new Vector3(1f, 1f, 0.15f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = 0.16f;
            Fade(particles, new Color(1f, 0.85f, 0.55f), new Color(1f, 0.42f, 0.12f), new Color(0.45f, 0.1f, 0.05f));
        }

        static void IceStream(ParticleSystem particles, Material trail)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 4.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(90f, 125f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = new Color(0.8f, 0.92f, 1f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 45f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 1.6f;
            shape.radius = 12f;
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 1f;
            collision.dampen = 1f;
            var trails = particles.trails;
            trails.enabled = true;
            trails.lifetime = 0.12f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            trails.dieWithParticles = true;
            particles.GetComponent<ParticleSystemRenderer>().trailMaterial = trail;
        }

        static void Volcanoes(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // A child of Earth: its sizes and speeds are in Earth's own units (one unit is Earth's diameter).
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = particles.emission;
            emission.rateOverTime = 120f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.51f;
            shape.radiusThickness = 0f;
            Fade(particles, new Color(1f, 0.75f, 0.4f), new Color(1f, 0.38f, 0.1f), new Color(0.3f, 0.12f, 0.06f));
        }

        static void Wind(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = 9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(32f, 40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new Color(0.7f, 0.82f, 1f, 0.22f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 160f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 46f;
            shape.radiusThickness = 1f;
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 1f;
            collision.dampen = 1f;
            var forces = particles.externalForces;
            forces.enabled = true;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
        }

        static void DigitStream(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
            main.startColor = new Color(1f, 0.92f, 0.7f);
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.enabled = false;
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        /// <summary>White-hot to red, fading out.</summary>
        static void Fade(ParticleSystem particles, Color hot, Color warm, Color cool)
        {
            var colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(hot, 0f), new GradientColorKey(warm, 0.35f), new GradientColorKey(cool, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }

        // ---- Assets ----

        /// <summary>
        /// The stars: directions, a third of them crowded toward the galaxy's plane (the band the sky
        /// draws); most faint, a few bright; mostly white, some blue, some orange.
        /// </summary>
        static Mesh MakeStars(int count, int seed)
        {
            var random = new System.Random(seed);
            var band = new Vector3(0.35f, 0.86f, 0.36f).normalized;
            var positions = new List<Vector3>(count * 4);
            var corners = new List<Vector2>(count * 4);
            var looks = new List<Vector2>(count * 4);
            var colours = new List<Color>(count * 4);
            var triangles = new List<int>(count * 6);
            Color[] kinds = { new Color(0.72f, 0.82f, 1f), Color.white, Color.white, new Color(1f, 0.93f, 0.8f), new Color(1f, 0.8f, 0.6f) };
            for (int i = 0; i < count; i++)
            {
                var d = new Vector3(Gauss(random), Gauss(random), Gauss(random)).normalized;
                if (random.NextDouble() < 0.35) d = (d - band * (Vector3.Dot(d, band) * 0.85f)).normalized;
                float m = (float)random.NextDouble();
                float bright = 0.12f + 2.6f * Mathf.Pow(m, 7f);
                float size = Mathf.Lerp(0.16f, 0.45f, Mathf.Pow(m, 3f));
                var colour = kinds[random.Next(kinds.Length)];
                colour.a = (float)random.NextDouble();
                int first = positions.Count;
                foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                {
                    positions.Add(d);
                    corners.Add(corner);
                    looks.Add(new Vector2(size, bright));
                    colours.Add(colour);
                }
                triangles.AddRange(new[] { first, first + 2, first + 1, first + 2, first + 3, first + 1 });
            }
            return SaveMesh("Stars", mesh =>
            {
                mesh.SetVertices(positions);
                mesh.SetUVs(0, corners);
                mesh.SetUVs(1, looks);
                mesh.SetColors(colours);
                mesh.SetTriangles(triangles, 0);
                // Drawn around the camera wherever it is, so it must never be culled.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            });
        }

        static float Gauss(System.Random random) =>
            Mathf.Sqrt(-2f * Mathf.Log(1f - (float)random.NextDouble())) * Mathf.Cos(2f * Mathf.PI * (float)random.NextDouble());

        /// <summary>Four tiles, a 1 and a 0 twice over, for the stream of digits.</summary>
        static Color Digits(float u, float v)
        {
            int tile = (int)(u * 2f) + (int)(v * 2f);
            var p = new Vector2(u * 2f % 1f, v * 2f % 1f);
            float d;
            if (tile % 2 == 0)
            {
                d = Mathf.Min(Segment(p, new Vector2(0.52f, 0.2f), new Vector2(0.52f, 0.8f)),
                    Mathf.Min(Segment(p, new Vector2(0.52f, 0.8f), new Vector2(0.36f, 0.66f)), Segment(p, new Vector2(0.38f, 0.2f), new Vector2(0.66f, 0.2f))));
            }
            else
            {
                var q = new Vector2((p.x - 0.5f) / 0.21f, (p.y - 0.5f) / 0.31f);
                d = Mathf.Abs(q.magnitude - 1f) * 0.24f;
            }
            float a = Mathf.Clamp01((0.075f - d) / 0.03f);
            return new Color(1f, 1f, 1f, a);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static VolumeProfile MakeYoungSunProfile()
        {
            string path = $"{Root}/Settings/Young Sun Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.75f);
            bloom.highQualityFiltering.Override(true);
            AddOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var grading = AddOverride<ColorAdjustments>(profile);
            grading.contrast.Override(8f);
            grading.saturation.Override(5f);
            AddOverride<ChromaticAberration>(profile).intensity.Override(0.05f);
            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            var grain = AddOverride<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.15f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ---- Wiring ----

        static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            (serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no field '{field}'")).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Fills a serialized array (of structs or strings) from a list.</summary>
        static void SetStructs<T>(Object target, string field, IList<T> items, Action<SerializedProperty, T> fill)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no field '{field}'");
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) fill(array.GetArrayElementAtIndex(i), items[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
