using UnityEngine;

namespace Game
{
    /// <summary>
    /// A thing in the world with a name: what the reticle shows when the player looks at it, and what
    /// the mind is told is there (an id the model may use, a few words about it). Its position is
    /// where things are built at or toward; <see cref="top"/>, if set, is where one would stand on it.
    /// </summary>
    public class Landmark : MonoBehaviour
    {
        [SerializeField, Tooltip("Upper case, as the model writes it: FAR_SIDE, EMBER, SPIRE.")] string id = "THING";
        [SerializeField, Tooltip("A few words, as the player would say it: the far side.")] string title = "a thing";
        [SerializeField, Tooltip("What the mind is told about it.")] string about = "";
        [SerializeField, Tooltip("Where one would stand on it; the object itself if empty.")] Transform top;
        [SerializeField, Tooltip("Shown and told only while this is on.")] bool known = true;

        public string Id => id;
        public string Title => title;
        public string About => about;
        public bool Known { get => known && isActiveAndEnabled; set => known = value; }
        public Vector3 Point => top ? top.position : transform.position;

        public void Set(string newId, string newTitle, string newAbout, Transform standOn = null)
        {
            id = newId;
            title = newTitle;
            about = newAbout;
            top = standOn;
        }
    }
}
