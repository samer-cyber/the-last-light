using UnityEngine;

namespace Game
{
    /// <summary>
    /// A pod's little panel: its screen and its lamp. In alarm the lamp blinks red with its light; once quiet the
    /// screen shows what it says now and the lamp stays lit.
    /// </summary>
    public class PanelScreen : MonoBehaviour
    {
        [SerializeField] Renderer screen, lamp;
        [SerializeField] Light lampLight;
        [SerializeField, Tooltip("What the screen shows once it is quiet.")] Material quiet;
        bool alarm;

        public void Alarm(bool on)
        {
            alarm = on;
            if (on) return;
            if (lamp) lamp.enabled = true;
            if (lampLight) lampLight.intensity = 0f;
            if (screen && quiet) screen.sharedMaterial = quiet;
        }

        void Update()
        {
            if (!alarm) return;
            bool lit = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
            if (lamp) lamp.enabled = lit;
            if (lampLight) lampLight.intensity = lit ? 1.2f : 0.05f;
        }
    }
}
