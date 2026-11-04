using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Anything warm the machines can smell: the survivor, a fire, a decoy, a flare. They go for the
    /// strongest within their reach, weighed by how near it is.
    /// </summary>
    public class HeatSource : MonoBehaviour
    {
        public static readonly List<HeatSource> All = new List<HeatSource>();

        [Tooltip("How strongly it draws them: the survivor 1, a fire 1.5, a decoy 2.5, a flare 4.")] public float Pull = 1f;
        [Tooltip("When above 0, it stops drawing them at this time.")] public float Until;
        public bool IsSurvivor;

        public bool Active => isActiveAndEnabled && (Until <= 0f || Time.time < Until);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>What draws a machine at a point most, within its reach.</summary>
        public static HeatSource Strongest(Vector3 from, float reach)
        {
            HeatSource best = null;
            float bestScore = 0f;
            foreach (var h in All)
            {
                if (!h || !h.Active) continue;
                float d = Vector3.Distance(h.transform.position, from);
                if (d > reach * Mathf.Sqrt(h.Pull)) continue;
                float score = h.Pull / Mathf.Max(4f, d);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            return best;
        }
    }
}
