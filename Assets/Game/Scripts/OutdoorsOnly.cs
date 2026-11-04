using UnityEngine;

namespace Game
{
    /// <summary>
    /// Weather that belongs to the open sky (falling snow, snow blown along the ground): it stops while the camera
    /// is under a roof (an <see cref="Indoors"/> box) and starts again outside. What has already fallen drifts out
    /// of sight on its own, so walking through a door never makes the snow vanish at once.
    /// </summary>
    public class OutdoorsOnly : MonoBehaviour
    {
        ParticleSystem particles;
        Light lamp;
        float full, shown;
        Indoors[] roofs;
        bool inside;

        void Start()
        {
            particles = GetComponent<ParticleSystem>();
            lamp = GetComponent<Light>();
            if (lamp) full = lamp.intensity;
            roofs = FindObjectsByType<Indoors>(FindObjectsSortMode.None);
            inside = Under();
            Set(inside);
            shown = inside ? 0f : 1f;
            if (lamp) lamp.intensity = full * shown;
            // Begun indoors (the waking in the vault): nothing should be falling in the hall.
            if (inside && particles) particles.Clear(true);
        }

        void Update()
        {
            bool now = Under();
            if (lamp)
            {
                shown = Mathf.MoveTowards(shown, now ? 0f : 1f, Time.deltaTime / 0.8f);
                lamp.intensity = full * shown;
                lamp.enabled = shown > 0.001f;
            }
            if (now == inside) return;
            inside = now;
            Set(inside);
        }

        bool Under()
        {
            var view = Camera.main;
            if (!view || roofs == null) return false;
            foreach (var roof in roofs)
                if (roof && roof.Holds(view.transform.position)) return true;
            return false;
        }

        void Set(bool under)
        {
            if (!particles) return;
            var emission = particles.emission;
            emission.enabled = !under;
        }
    }
}
