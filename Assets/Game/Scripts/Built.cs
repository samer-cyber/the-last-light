using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>A thing the mind made: its pieces, what they are made of, and the words that asked for it.</summary>
    public class Built : MonoBehaviour
    {
        public readonly List<GameObject> Pieces = new List<GameObject>();
        public string Kind { get; private set; }
        public Matter Matter { get; private set; }
        public int Units { get; private set; }
        public string Words { get; private set; }

        public void Set(string kind, Matter matter, int units, string words)
        {
            Kind = kind;
            Matter = matter;
            Units = units;
            Words = words;
        }
    }
}
