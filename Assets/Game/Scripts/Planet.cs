using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A planet's look, eased toward what it is becoming: the amounts its surface shows (molten rock,
    /// seas, steam, clouds, ice; see Planet.shader) and its air's colour, thickness and auroras (see
    /// Atmosphere.shader). It turns on its axis, faster while time is hurried.
    /// </summary>
    public class Planet : MonoBehaviour
    {
        [SerializeField] Renderer surface;
        [SerializeField, Tooltip("Optional: the shell of air around it.")] Renderer air;
        [SerializeField, Tooltip("Turns per minute.")] float spin = 0.5f;

        /// <summary>Multiplies the turning while time is hurried.</summary>
        public float Hurry { get; set; } = 1f;
        public Renderer Surface => surface;

        class Tween
        {
            public Material material;
            public int id;
            public bool colour;
            public Vector4 from, to;
            public float start, length;
        }

        readonly List<Tween> tweens = new List<Tween>();
        Material surfaceMaterial, airMaterial;

        void Awake()
        {
            surfaceMaterial = surface.material;
            if (air) airMaterial = air.material;
        }

        /// <summary>Eases one of the surface's amounts (for example "_Molten") to a value.</summary>
        public void Ease(string property, float to, float seconds) => Animate(surfaceMaterial, property, new Vector4(to, 0f, 0f, 0f), false, seconds);

        /// <summary>Eases one of the air's amounts ("_Thickness", "_Aurora").</summary>
        public void EaseAir(string property, float to, float seconds)
        {
            if (airMaterial) Animate(airMaterial, property, new Vector4(to, 0f, 0f, 0f), false, seconds);
        }

        /// <summary>Eases the air's colour.</summary>
        public void EaseHaze(Color to, float seconds)
        {
            if (airMaterial) Animate(airMaterial, "_Haze", to, true, seconds);
        }

        public float Get(string property) => surfaceMaterial.GetFloat(property);

        void Animate(Material material, string property, Vector4 to, bool colour, float seconds)
        {
            int id = Shader.PropertyToID(property);
            tweens.RemoveAll(t => t.material == material && t.id == id);
            Vector4 from = colour ? (Vector4)material.GetColor(id) : new Vector4(material.GetFloat(id), 0f, 0f, 0f);
            if (seconds <= 0f)
            {
                Apply(material, id, colour, to);
                return;
            }
            tweens.Add(new Tween { material = material, id = id, colour = colour, from = from, to = to, start = Time.time, length = seconds });
        }

        void Update()
        {
            // West to east, counter-clockwise seen from above the north pole.
            transform.Rotate(0f, -spin * 6f * Hurry * Time.deltaTime, 0f, Space.Self);
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                var tween = tweens[i];
                float k = Mathf.Clamp01((Time.time - tween.start) / tween.length);
                Apply(tween.material, tween.id, tween.colour, Vector4.Lerp(tween.from, tween.to, k * k * (3f - 2f * k)));
                if (k >= 1f) tweens.RemoveAt(i);
            }
        }

        static void Apply(Material material, int id, bool colour, Vector4 value)
        {
            if (colour) material.SetColor(id, value);
            else material.SetFloat(id, value.x);
        }
    }
}
