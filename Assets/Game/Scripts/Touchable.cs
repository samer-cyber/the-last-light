using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Something the survivor can touch with [E]: a panel, a switch. The aim says what touching does;
    /// whoever listens hears when it is touched. When it is not ready, it is only a thing.
    /// </summary>
    public class Touchable : MonoBehaviour
    {
        [SerializeField] string title = "the panel";
        [SerializeField, Tooltip("What [E] does, as the aim shows it: \"touch the panel\".")] string verb = "touch the panel";
        [SerializeField] bool ready;

        public event Action Touched;
        public string Title => title;
        public string Verb => verb;
        /// <summary>Whether [E] does anything now.</summary>
        public bool Ready { get => ready; set => ready = value; }

        public void Touch()
        {
            if (!ready) return;
            Touched?.Invoke();
        }
    }
}
