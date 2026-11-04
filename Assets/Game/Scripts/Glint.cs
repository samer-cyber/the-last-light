using UnityEngine;

namespace Game
{
    /// <summary>
    /// A faint glint that breathes: mostly dim, now and then a brief bright catch of light (the sun mirror on the
    /// clifftop, seen from far off).
    /// </summary>
    public class Glint : MonoBehaviour
    {
        [SerializeField] float low = 0.9f, high = 2.8f, period = 5f;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        Renderer look;
        MaterialPropertyBlock block;

        void Awake()
        {
            look = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!look) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period);
            look.GetPropertyBlock(block);
            block.SetFloat(IntensityId, Mathf.Lerp(low, high, k * k * k));
            look.SetPropertyBlock(block);
        }
    }
}
