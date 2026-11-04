using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Moves the traveller's jointed body by hand, every frame, from how they move: a walk or a run
    /// whose stride follows the ground covered, arms swinging against the legs, a lean into speed,
    /// legs drawn up in the air and a dip on landing, breathing when still, a hand out to gather, the
    /// head turned toward what they look at, and the cloak dragging behind. No animation clips.
    /// </summary>
    public class TravellerBody : MonoBehaviour
    {
        [Serializable]
        public struct Side
        {
            public Transform upperArm, forearm, thigh, shin, foot;
        }

        [SerializeField] Traveller traveller;
        [SerializeField] Transform hips, spine, chest, neck, head, cloak;
        [SerializeField] Side left, right;
        [SerializeField, Tooltip("Metres covered by one step at a walk, and at a run.")] Vector2 stride = new Vector2(0.75f, 1.25f);
        [SerializeField] Transform lookAt;

        /// <summary>A step landed: which foot (0 left, 1 right) and the pace (0 to 2).</summary>
        public event Action<int, float> Stepped;

        Vector3 hipsRest;
        float phase, landDip, lean, breath, cloakSwing, cloakVelocity, settled;
        int lastStep = -1;
        Quaternion headRest, neckRest;

        void Start()
        {
            hipsRest = hips.localPosition;
            headRest = head.localRotation;
            neckRest = neck.localRotation;
            traveller.Landed += air => landDip = Mathf.Clamp(air * 0.25f, 0.04f, 0.16f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 velocity = traveller.Velocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            float pace = traveller.Pace;
            bool air = !traveller.Grounded;
            float run = Mathf.Clamp01(pace - 1f);
            float moving = Mathf.Clamp01(pace);

            // The stride: a full cycle is two steps.
            float stepLength = Mathf.Lerp(stride.x, stride.y, run);
            if (!air) phase += speed * dt / stepLength * Mathf.PI;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            int step = Mathf.FloorToInt(phase / Mathf.PI);
            if (step != lastStep && moving > 0.3f && !air)
            {
                Stepped?.Invoke(step & 1, pace);
                lastStep = step;
            }

            float swing = Mathf.Lerp(26f, 44f, run) * moving;
            float knee = Mathf.Lerp(38f, 72f, run) * moving;
            float armSwing = Mathf.Lerp(22f, 48f, run) * moving;
            breath += dt;

            // Legs: a leg swinging forward bends at the knee; in the air both are drawn up. Standing, the weight
            // rests on one leg and slowly shifts to the other, both knees soft.
            settled = Mathf.MoveTowards(settled, air ? 0f : 1f - moving, dt * 2f);
            float shift = Mathf.Sin(breath * 0.32f);
            Leg(left, s * swing - settled * 2f, Mathf.Max(0f, c) * knee + 6f * moving + settled * (5f + 4f * shift), air);
            Leg(right, -s * swing - settled * 2f, Mathf.Max(0f, -c) * knee + 6f * moving + settled * (5f - 4f * shift), air);

            // Arms against the legs; bent more when running; one reaches out when gathering.
            float reach = Mathf.Clamp01(1f - (Time.time - traveller.ReachedAt) / 0.7f);
            reach = Mathf.Sin(reach * Mathf.PI);
            // Something carried in both hands holds both arms out in front; a tool rides in the
            // right hand and swings with each blow; a balloon's cord pulls the right arm up.
            var held = traveller.Holding;
            int both = held && !held.OneHand ? 1 : 0;
            int pose = held && held.OneHand ? traveller.Floating ? 3 : 2 : both;
            var weapon = held ? held.GetComponent<Weapon>() : null;
            if (weapon && weapon.Type != Weapon.Kind.Blade) pose = 4;
            else if (held && held.GetComponent<Shield>()) pose = 5;
            else if (held && held.GetComponent<Glider>()) pose = 3;
            Arm(left, both == 1 ? 0f : -s * armSwing, run, air, 0f, both);
            Arm(right, pose != 0 ? 0f : s * armSwing, run, air, reach, pose);

            // Body: a bob twice a step, a lean into speed, a dip on landing, breathing when still.
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 0.5f);
            float bob = moving * (Mathf.Abs(c) * Mathf.Lerp(0.035f, 0.06f, run) - 0.03f);
            hips.localPosition = hipsRest + new Vector3(0f, bob - landDip * 2.5f, 0f);
            hips.localRotation = Quaternion.Euler(0f, s * 7f * moving, -c * 3f * moving + settled * shift * 2.2f);
            lean = Mathf.Lerp(lean, Mathf.Lerp(3f, 13f, run) * moving + (air ? 8f : 0f), 1f - Mathf.Exp(-5f * dt));
            float still = 1f - moving;
            spine.localRotation = Quaternion.Euler(lean * 0.5f + landDip * 60f, -s * 5f * moving, 0f);
            chest.localRotation = Quaternion.Euler(lean * 0.5f + still * Mathf.Sin(breath * 1.6f) * 1.6f, -s * 6f * moving, 0f);

            // The head turns toward what the traveller looks at, within a neck's reach.
            Vector3 lookPoint = traveller.Attending ?? traveller.AimPoint ?? (lookAt ? lookAt.position : head.position + transform.forward);
            Vector3 toLook = (lookPoint - head.position).normalized;
            Vector3 local = transform.InverseTransformDirection(toLook);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -35f, 40f);
            var turn = Quaternion.Euler(pitch * 0.5f, yaw * 0.5f, 0f);
            neck.localRotation = Quaternion.Slerp(neck.localRotation, neckRest * turn, 1f - Mathf.Exp(-6f * dt));
            head.localRotation = Quaternion.Slerp(head.localRotation, headRest * turn, 1f - Mathf.Exp(-6f * dt));

            // The cloak drags behind with speed and swings like a pendulum when it changes.
            float drag = Mathf.Clamp(speed * 3.2f + (air ? -velocity.y * 2f : 0f), -25f, 32f);
            float force = (drag - cloakSwing) * 40f;
            cloakVelocity = (cloakVelocity + force * dt) * Mathf.Exp(-4.5f * dt);
            cloakSwing += cloakVelocity * dt;
            if (cloak) cloak.localRotation = Quaternion.Euler(cloakSwing - lean, 0f, Mathf.Sin(breath * 0.9f + phase * 0.5f) * (1.5f + 2f * moving));
        }

        void Leg(Side side, float thigh, float knee, bool air)
        {
            if (air)
            {
                thigh = Mathf.Lerp(thigh, side.thigh == left.thigh ? 34f : 12f, 0.7f);
                knee = Mathf.Lerp(knee, side.thigh == left.thigh ? 70f : 40f, 0.7f);
            }
            float dip = landDip * 260f;
            side.thigh.localRotation = Quaternion.Euler(-thigh - dip * 0.5f, 0f, 0f);
            side.shin.localRotation = Quaternion.Euler(knee + dip, 0f, 0f);
            side.foot.localRotation = Quaternion.Euler(thigh * 0.6f - knee * 0.55f - dip * 0.5f, 0f, 0f);
        }

        /// <summary>How far the view looks down (degrees; up is negative), for aiming.</summary>
        float Aim => traveller && traveller.Follow ? Mathf.Clamp(traveller.Follow.Pitch, -40f, 50f) : 0f;

        void Arm(Side side, float swing, float run, bool air, float reach, int pose)
        {
            bool isLeft = side.upperArm == left.upperArm;
            float outward = isLeft ? -1f : 1f;
            float lift = air && pose == 0 ? 30f : 0f;
            // Hanging easy when still: close to the body, a little forward, the elbows soft, swaying with the breath.
            float hang = settled * (4f + Mathf.Sin(breath * 1.6f + (isLeft ? 0f : 1.3f)) * 1.2f);
            var arm = Quaternion.Euler(-swing - lift - hang, outward * -settled * 6f, outward * (8f - settled * 3.5f + lift * 0.4f));
            float elbow = Mathf.Lerp(14f, 70f, run) + (air ? 30f : 0f) + settled * 8f;
            switch (pose)
            {
                case 1:
                    // Carrying in front, both hands under it.
                    arm = Quaternion.Euler(-48f, outward * -10f, outward * 6f);
                    elbow = 62f;
                    break;
                case 2:
                    // A tool in hand, raised for a blow and brought down when one is struck.
                    arm = Quaternion.Euler(Mathf.Lerp(-40f, -125f, reach), 0f, outward * 10f);
                    elbow = Mathf.Lerp(48f, 10f, reach);
                    reach = 0f;
                    break;
                case 3:
                    // Hanging from a balloon's cord.
                    arm = Quaternion.Euler(-172f, 0f, outward * 8f);
                    elbow = 4f;
                    break;
                case 4:
                    // Held out and aimed where the view looks, a kick back when it fires.
                    arm = Quaternion.Euler(-86f + Aim * 0.85f + reach * 14f, outward * -4f, outward * 3f);
                    elbow = 6f + reach * 18f;
                    reach = 0f;
                    break;
                case 5:
                    // A shield before the chest.
                    arm = Quaternion.Euler(-62f, outward * -28f, outward * 4f);
                    elbow = 74f;
                    break;
            }
            if (reach > 0f) arm = Quaternion.Slerp(arm, Quaternion.Euler(-70f, outward * -15f, outward * 10f), reach);
            side.upperArm.localRotation = arm;
            side.forearm.localRotation = Quaternion.Euler(-Mathf.Lerp(elbow, 6f, reach), 0f, 0f);
        }
    }
}
