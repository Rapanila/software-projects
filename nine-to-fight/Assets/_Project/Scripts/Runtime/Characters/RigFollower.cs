using System;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Ties the imported skinned skeletons to the rest of the character. One rig is active at a time (the
    /// body CharacterLook picked).
    /// With animation clips (the normal case) the Animator poses the skeleton; this closes both hands into
    /// fists and seats the hand socket in the weapon fist, with the held item running across the palm and
    /// out between thumb and little finger.
    /// Without clips it falls back to the procedural rig: each invisible pivot's rotation is copied onto
    /// its bone (a pivot can drive a chain, each link taking a share of the bend).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RigFollower : MonoBehaviour
    {
        [Serializable]
        public class Rig
        {
            public GameObject root;
            [Tooltip("Plays the animation clips on this body.")]
            public Animator animator;
            [Tooltip("Driven bones, parents before children.")]
            public Transform[] bones;
            [Tooltip("The procedural pivot that drives each bone.")]
            public Transform[] drivers;
            [Tooltip("Bone rotation relative to its driver, captured in the rest pose.")]
            public Quaternion[] offsets;
            [Tooltip("Share of the driver's bend this bone takes (1 = all of it; lower links of a chain take less).")]
            public float[] weights;
            public Vector3[] driverRest;
            public Vector3[] boneRest;
            [Tooltip("Weapon-hand bone, and the grip (centre of the closed fist) in its space.")]
            public Transform hand;
            public Vector3 grip;
            [Tooltip("Item orientation for weapon swings (out past the thumb), relative to the hand bone.")]
            public Quaternion gripRotation = Quaternion.identity;
            [Tooltip("Item orientation for the guard and punches (out the front of the fist, away from the face).")]
            public Quaternion punchRotation = Quaternion.identity;
            [Tooltip("Finger bones of both hands, and their closed-fist rotations.")]
            public Transform[] fingers;
            public Quaternion[] fist;
            [Tooltip("Upper spine and neck, jolted by hits on top of the animation.")]
            public Transform chest, neck;
            [Tooltip("The way the face points, in the head bone's space.")]
            public Vector3 headForward = Vector3.forward;
        }

        [SerializeField] Rig[] rigs;
        [SerializeField] Transform handSocket;
        [Tooltip("How much of the pivots' shoulder reach moves the real shoulders.")]
        [SerializeField, Range(0f, 1f)] float shoulderTravel = 0.6f;

        int active;

        public int RigCount => rigs != null ? rigs.Length : 0;
        public int Active => active;
        public Rig ActiveRig => rigs != null && rigs.Length > 0 ? rigs[active] : null;
        /// <summary>The weapon hand of the active body.</summary>
        public Transform Hand => ActiveRig?.hand;
        /// <summary>Set while animation clips pose the skeleton (no procedural bone driving).</summary>
        public bool ClipDriven { get; set; }
        /// <summary>0 = punch grip (guard, punches, kicks), 1 = swing grip (weapon swings).</summary>
        public float SwingGrip { get; set; }

#if UNITY_EDITOR
        /// <summary>Content builder hook.</summary>
        public void Configure(Rig[] rigs, Transform handSocket)
        {
            this.rigs = rigs;
            this.handSocket = handSocket;
        }
#endif

        public void Select(int index)
        {
            if (rigs == null || rigs.Length == 0) return;
            active = Mathf.Clamp(index, 0, rigs.Length - 1);
            for (int i = 0; i < rigs.Length; i++)
                if (rigs[i].root != null) rigs[i].root.SetActive(i == active);
            LateUpdate();
        }

        void LateUpdate()
        {
            var rig = ActiveRig;
            if (rig == null || rig.root == null || !rig.root.activeInHierarchy) return;

            if (ClipDriven)
            {
                if (rig.fingers != null)
                    for (int i = 0; i < rig.fingers.Length; i++)
                        if (rig.fingers[i] != null) rig.fingers[i].localRotation = rig.fist[i];
                if (handSocket != null && rig.hand != null)
                {
                    handSocket.position = rig.hand.TransformPoint(rig.grip);
                    handSocket.rotation = rig.hand.rotation * Quaternion.Slerp(rig.punchRotation, rig.gripRotation, SwingGrip);
                }
                return;
            }

            for (int i = 0; i < rig.bones.Length; i++)
            {
                var bone = rig.bones[i];
                var driver = rig.drivers[i];
                if (bone == null || driver == null) continue;
                // The hand socket is re-seated on the hand below, so only its rotation drives the wrist.
                Vector3 reach = driver == handSocket ? Vector3.zero : driver.localPosition - rig.driverRest[i];
                if (reach.sqrMagnitude > 1e-8f && driver.parent != null && bone.parent != null)
                {
                    Vector3 world = driver.parent.TransformVector(reach) * shoulderTravel;
                    bone.localPosition = rig.boneRest[i] + bone.parent.InverseTransformVector(world);
                }
                else
                {
                    bone.localPosition = rig.boneRest[i];
                }
                // Pivots rest at identity, so their local rotation is the bend; a chain link takes its share of it.
                float weight = rig.weights != null && i < rig.weights.Length ? rig.weights[i] : 1f;
                bone.rotation = weight >= 0.999f || driver.parent == null
                    ? driver.rotation * rig.offsets[i]
                    : driver.parent.rotation * Quaternion.Slerp(Quaternion.identity, driver.localRotation, weight) * rig.offsets[i];
            }

            if (handSocket != null && rig.hand != null) handSocket.position = rig.hand.TransformPoint(rig.grip);
        }
    }
}
