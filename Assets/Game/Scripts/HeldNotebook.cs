using UnityEngine;

namespace Game
{
    /// <summary>
    /// The notebook in the player's hands, as seen from their eyes: lowered out of sight, or raised in
    /// front of them with the pencil on the page. The pencil goes to where the next mark is made and
    /// scribbles as letters are written. The orb's light brightens the page.
    /// </summary>
    public class HeldNotebook : MonoBehaviour
    {
        static readonly int LightId = Shader.PropertyToID("_Light");

        [SerializeField] Transform notebook;
        [SerializeField, Tooltip("The page quad; the notes are drawn on it.")] Renderer page;
        [SerializeField] Transform pencil;
        [SerializeField] Orb orb;
        [SerializeField] Vector3 raisedPosition = new Vector3(0.07f, 0.05f, 0.21f);
        [SerializeField] Vector3 raisedRotation = new Vector3(16f, 14f, 3f);
        [SerializeField] Vector3 loweredPosition = new Vector3(0.08f, -0.42f, 0.2f);
        [SerializeField] Vector3 loweredRotation = new Vector3(72f, -10f, 12f);
        [SerializeField] float raiseSeconds = 0.45f;
        [SerializeField, Tooltip("How the pencil leans from its tip, in the page's own axes.")] Vector3 pencilLean = new Vector3(0.55f, -0.45f, -0.7f);

        public bool Raised { get; private set; }
        public float RaiseSeconds => raiseSeconds;

        Material pageMaterial;
        Vector3 pageCentre, pageSize;
        float shown, shownSpeed, scribble;
        Vector2 pencilAt = new Vector2(0.75f, 0.35f), pencilGoal = new Vector2(0.75f, 0.35f);
        Vector2 pencilSpeed;

        void Awake()
        {
            pageMaterial = page.material;
            var bounds = page.GetComponent<MeshFilter>().sharedMesh.bounds;
            pageCentre = bounds.center;
            pageSize = bounds.size;
            Pose(0f);
        }

        public void Raise() => Raised = true;

        public void Lower() => Raised = false;

        /// <summary>Sends the pencil to a point on the page (0..1 from the bottom left); scribbles if writing.</summary>
        public void WriteAt(Vector2 pageUv, bool writing)
        {
            pencilGoal = pageUv;
            if (writing) scribble = 1f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            shown = Mathf.SmoothDamp(shown, Raised ? 1f : 0f, ref shownSpeed, raiseSeconds * 0.3f);
            bool visible = shown > 0.002f;
            if (notebook.gameObject.activeSelf != visible) notebook.gameObject.SetActive(visible);
            if (pencil.gameObject.activeSelf != visible) pencil.gameObject.SetActive(visible);
            if (!visible) return;
            Pose(shown);

            pencilAt = Vector2.SmoothDamp(pencilAt, pencilGoal, ref pencilSpeed, 0.08f);
            scribble = Mathf.MoveTowards(scribble, 0f, dt * 4f);
            PlacePencil();

            float light = orb ? 0.72f + 0.4f * orb.Pulse : 0.8f;
            pageMaterial.SetFloat(LightId, light);
        }

        void Pose(float t)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            float time = Time.time;
            // Held, the hands breathe a little.
            var sway = new Vector3(Mathf.Sin(time * 0.8f) * 0.0025f, Mathf.Sin(time * 1.1f + 0.7f) * 0.003f, 0f) * e;
            notebook.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, e) + sway;
            notebook.localRotation = Quaternion.Slerp(Quaternion.Euler(loweredRotation), Quaternion.Euler(raisedRotation), e);
        }

        void PlacePencil()
        {
            var pageTransform = page.transform;
            float t = Time.time;
            var wiggle = new Vector2(Mathf.Sin(t * 47f), Mathf.Sin(t * 31f + 1.3f)) * (0.006f * scribble);
            var uv = pencilAt + new Vector2(wiggle.x / pageSize.x, wiggle.y / pageSize.y) * 0.5f;
            var local = pageCentre + new Vector3((uv.x - 0.5f) * pageSize.x, (uv.y - 0.5f) * pageSize.y, -0.0015f);
            Vector3 tip = pageTransform.TransformPoint(local);
            Vector3 lean = pageTransform.TransformDirection(pencilLean.normalized);
            // The pencil's tip is at its +Z end, 0.076 m from its middle; lift it a little between strokes.
            float lift = 0.004f * (1f - scribble);
            pencil.position = tip + lean * 0.076f - pageTransform.forward * lift;
            pencil.rotation = Quaternion.LookRotation(-lean, pageTransform.up);
        }
    }
}
