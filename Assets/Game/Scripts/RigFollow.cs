using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The survivor's look: a rigged model whose skeleton follows the jointed figure that
    /// <see cref="TravellerBody"/> moves by hand (walk, run, carry, a tool, a cord, the head turned
    /// to look). At the start each bone is lined up with its joint's rest (limbs hanging, the spine
    /// straight); from then on it takes its joint's turn. The figure itself is not drawn.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class RigFollow : MonoBehaviour
    {
        [Serializable]
        public struct Link
        {
            [Tooltip("The figure's joint, turned by TravellerBody.")] public Transform joint;
            [Tooltip("The model's bone that follows it.")] public Transform bone;
            [Tooltip("The bone the limb points at (its child), to line it up.")] public Transform next;
            [Tooltip("Which way the joint's limb points at rest, in the body's space: down for arms and legs, up for the spine.")] public Vector3 rest;
        }

        [SerializeField] Transform body;
        [SerializeField, Tooltip("Parents first.")] Link[] links = new Link[0];
        [SerializeField] Transform hipsJoint, hipsBone;
        [SerializeField, Tooltip("Where held things hang from, moved onto the model's hand.")] Transform hand, handBone;

        Quaternion[] offsets;
        Vector3 hipsJointRest, hipsBoneRest;

        void Start()
        {
            foreach (var l in links)
            {
                if (!l.bone || !l.next || l.rest == Vector3.zero) continue;
                Vector3 now = l.next.position - l.bone.position;
                if (now.sqrMagnitude < 1e-8f) continue;
                l.bone.rotation = Quaternion.FromToRotation(now.normalized, body.TransformDirection(l.rest).normalized) * l.bone.rotation;
            }
            offsets = new Quaternion[links.Length];
            for (int i = 0; i < links.Length; i++)
                offsets[i] = links[i].joint && links[i].bone ? Quaternion.Inverse(links[i].joint.rotation) * links[i].bone.rotation : Quaternion.identity;
            if (hipsJoint && hipsBone)
            {
                hipsJointRest = body.InverseTransformPoint(hipsJoint.position);
                hipsBoneRest = body.InverseTransformPoint(hipsBone.position);
            }
            if (hand && handBone)
            {
                hand.SetParent(handBone, true);
                hand.position = handBone.position + body.forward * 0.03f + Vector3.down * 0.06f;
            }
        }

        void LateUpdate()
        {
            if (offsets == null) return;
            if (hipsJoint && hipsBone) hipsBone.position = body.TransformPoint(hipsBoneRest + body.InverseTransformPoint(hipsJoint.position) - hipsJointRest);
            for (int i = 0; i < links.Length; i++)
                if (links[i].joint && links[i].bone) links[i].bone.rotation = links[i].joint.rotation * offsets[i];
        }
    }
}
