using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A stone door in a ruin wall, held shut until enough weight rests on the plate in front of it.
    /// The traveller alone is too light; a made block, a ball or anything heavy will do. The plate sinks
    /// under weight and the door slides into the ground; take the weight off and it rises again.
    /// </summary>
    public class Gate : MonoBehaviour
    {
        [SerializeField] Transform door;
        [SerializeField] Transform plate;
        [SerializeField, Tooltip("The trigger over the plate.")] BoxCollider pressing;
        [SerializeField] Transform traveller;
        [SerializeField, Tooltip("Kilograms the plate needs.")] float needs = 250f;
        [SerializeField, Tooltip("What the traveller counts as.")] float travellerWeight = 70f;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip moveClip;

        public event Action<bool> Moved;
        public bool Open { get; private set; }
        /// <summary>Kilograms on the plate now.</summary>
        public float Load { get; private set; }

        Vector3 doorShut, plateUp;
        float opening, pressed;
        readonly Collider[] touching = new Collider[32];

        void Start()
        {
            doorShut = door.localPosition;
            plateUp = plate.localPosition;
        }

        void FixedUpdate()
        {
            float load = 0f;
            var box = pressing.transform;
            int count = Physics.OverlapBoxNonAlloc(box.TransformPoint(pressing.center), Vector3.Scale(pressing.size * 0.5f, box.lossyScale), touching, box.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = touching[i].attachedRigidbody;
                if (body && !body.isKinematic) load += body.mass;
                else if (traveller && touching[i].transform == traveller) load += travellerWeight;
            }
            Load = load;
            bool open = load >= needs;
            if (open != Open)
            {
                Open = open;
                if (sound && moveClip) sound.PlayOneShot(moveClip, 0.8f);
                Moved?.Invoke(open);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            opening = Mathf.MoveTowards(opening, Open ? 1f : 0f, dt / 2.2f);
            pressed = Mathf.MoveTowards(pressed, Mathf.Clamp01(Load / needs), dt * 2f);
            float eased = opening * opening * (3f - 2f * opening);
            door.localPosition = doorShut + Vector3.down * eased * (door.localScale.y + 0.05f);
            plate.localPosition = plateUp + Vector3.down * pressed * 0.08f;
        }
    }
}
