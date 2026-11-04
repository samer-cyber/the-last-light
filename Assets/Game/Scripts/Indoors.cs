using UnityEngine;

namespace Game
{
    /// <summary>
    /// A sealed place the sky's light does not reach (the vault's hall): while the camera is inside its
    /// box, the ambient light eases to a cold, dim one and the sun's shadows go fully dark (outside they
    /// let some light through, which would leak through every wall in here), so only the lamps and Spark
    /// light it. Outside, both are left as whatever else sets them.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Indoors : MonoBehaviour
    {
        [SerializeField] Color sky = new Color(0.045f, 0.055f, 0.075f);
        [SerializeField] Color equator = new Color(0.03f, 0.035f, 0.045f);
        [SerializeField] Color ground = new Color(0.015f, 0.016f, 0.02f);
        [SerializeField, Tooltip("Seconds to ease in or out.")] float ease = 0.8f;

        BoxCollider box;
        Light sun;
        Color outSky, outEquator, outGround;
        float inside, outShadows = 1f;

        void Awake() => box = GetComponent<BoxCollider>();

        /// <summary>Whether a point is under this roof.</summary>
        public bool Holds(Vector3 point) => (box ? box : box = GetComponent<BoxCollider>()) && box.bounds.Contains(point);

        void Start()
        {
            sun = RenderSettings.sun;
            if (!sun)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && (!sun || l.intensity > sun.intensity)) sun = l;
        }

        void Update()
        {
            var view = Camera.main;
            bool within = view && box.bounds.Contains(view.transform.position);
            if (!within && inside <= 0f)
            {
                outSky = RenderSettings.ambientSkyColor;
                outEquator = RenderSettings.ambientEquatorColor;
                outGround = RenderSettings.ambientGroundColor;
                if (sun) outShadows = sun.shadowStrength;
                return;
            }
            inside = Mathf.MoveTowards(inside, within ? 1f : 0f, Time.deltaTime / ease);
            RenderSettings.ambientSkyColor = Color.Lerp(outSky, sky, inside);
            RenderSettings.ambientEquatorColor = Color.Lerp(outEquator, equator, inside);
            RenderSettings.ambientGroundColor = Color.Lerp(outGround, ground, inside);
            if (sun) sun.shadowStrength = Mathf.Lerp(outShadows, 1f, inside);
        }
    }
}
