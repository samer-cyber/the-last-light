using UnityEngine;

namespace Game
{
    /// <summary>
    /// The settings of every particle system in the opening, in one place. The scene builder
    /// applies them, so they are saved in the scene and can be seen and tuned in the inspector.
    /// </summary>
    public static class ParticleRecipes
    {
        /// <summary>A thin disc of dust turning around the orb and sinking inward; centre and half-width in metres.</summary>
        public static void Dust(ParticleSystem ps, float centre, float width)
        {
            Clear(ps);
            var main = ps.main;
            main.loop = true;
            main.duration = 26f;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.5f, 1f), new Color(0.4f, 0.75f, 1f));
            main.maxParticles = 7000;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 220f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = centre;
            shape.donutRadius = width;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.scale = new Vector3(1f, 1f, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // All three orbital curves must be in the same mode.
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.2f);

            Drift(ps, 0.12f, 0.3f);
            Fade(ps, 0.15f, 0.85f);
            Billboards(ps, 0.5f);
        }

        /// <summary>A sparse shell of fine dust all around the orb.</summary>
        public static void Haze(ParticleSystem ps, float radius)
        {
            Clear(ps);
            var main = ps.main;
            main.loop = true;
            main.duration = 18f;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.6f, 1f, 0.6f), new Color(0.5f, 0.8f, 1f, 0.6f));
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 60f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.6f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.15f);

            Drift(ps, 0.5f, 0.2f);
            Fade(ps, 0.2f, 0.8f);
            Billboards(ps, 0.5f);
        }

        /// <summary>Rings sent out one at a time by the orb with each dot and dash.</summary>
        public static void Rings(ParticleSystem ps)
        {
            Clear(ps);
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.playOnAwake = true;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize = 10f;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 3.2f, 3.2f), new Keyframe(1f, 2.6f, 0f, 0f)));

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            Billboards(ps, 10f);
        }

        /// <summary>The burst when the orb breaks: streaks of light thrown out in every direction.</summary>
        public static void Burst(ParticleSystem ps, float radius)
        {
            Clear(ps);
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 120f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.85f), new Color(0.6f, 0.75f, 1f));
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3000) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.15f;
            shape.radiusThickness = 1f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 8f;
            limit.dampen = 0.03f;

            Fade(ps, 0.02f, 0.4f);
            var renderer = Billboards(ps, 10f);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
        }

        /// <summary>Faint specks around the player, left behind as they move, so drifting can be felt.</summary>
        public static void Motes(ParticleSystem ps)
        {
            Clear(ps);
            var main = ps.main;
            main.loop = true;
            main.duration = 14f;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.8f, 1f, 0.6f));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 12f;
            shape.radiusThickness = 1f;

            Drift(ps, 0.08f, 0.3f);
            Fade(ps, 0.2f, 0.8f);
            var renderer = Billboards(ps, 0.02f);
            renderer.minParticleSize = 0.0008f;
        }

        static void Clear(ParticleSystem ps) => ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        static void Drift(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.04f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        static void Fade(ParticleSystem ps, float inUntil, float outFrom)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        static ParticleSystemRenderer Billboards(ParticleSystem ps, float maxScreenSize)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.maxParticleSize = maxScreenSize;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
