using UnityEngine;

namespace Game
{
    /// <summary>
    /// Something written or drawn in the world that the survivor can look at closely with [E]: a note in an empty
    /// pod, the protocols poster, the hall's status screen, a child's drawing, words on a wall. The page is shown
    /// large over the view until [E] or [Esc] puts it down; Spark is told what they read.
    /// </summary>
    public class Readable : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<Readable> All = new System.Collections.Generic.List<Readable>();

        [SerializeField] Texture2D page;
        [SerializeField] string title = "the note";
        [SerializeField, TextArea, Tooltip("What is written on it, word for word (Spark is told it; no photograph needed).")] string words;

        public Texture2D Page => page;
        public string Title => title;
        public string Words => words;
        /// <summary>Width over height of the page, so it is shown in its own shape.</summary>
        public float Aspect => page && page.height > 0 ? page.width / (float)page.height : 1f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Where it is and how big it looks, for finding it in the view.</summary>
        public Bounds Bounds
        {
            get
            {
                var shown = GetComponent<Renderer>();
                return shown ? shown.bounds : new Bounds(transform.position, Vector3.one * 0.3f);
            }
        }

        public void Set(Texture2D page, string title, string words = null)
        {
            this.page = page;
            this.title = title;
            this.words = words;
        }
    }
}
