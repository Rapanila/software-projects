using System;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>
    /// Ties the imported skinned skeletons to the rest of the character. One rig is active at a time (the
    /// body CharacterLook picked).
    /// With animation clips (the normal case) the Animator poses the skeleton; this applies the item's
    /// fitted finger pose and seats the hand socket in the holding hand. The grip stays fixed relative to the hand
    /// while the animated wrist drives the item through each attack.
    /// Without clips (edit-mode tooling and the fitting tests, where no Animator runs) each invisible pivot's
    /// rotation is copied onto its bone (a pivot can drive a chain, each link taking a share of the bend). The
    /// pivots rest at their bind pose, so this holds the body in its rest pose with the item seated in the hand.
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
            [Tooltip("Weapon-hand bone, and the grip (the hole the closed fist wraps around) in its space.")]
            public Transform hand;
            public Vector3 grip;
            [Tooltip("Item orientation relative to the hand bone, shared by idle poses and attacks.")]
            public Quaternion gripRotation = Quaternion.identity;
            [Tooltip("Finger bones of both hands, and their closed-fist rotations.")]
            public Transform[] fingers;
            public Quaternion[] fingerOpen;
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
        HeldItemDisplay heldItem;
        CharacterVisual visual;
        float carryWeight;
        bool carriedSocket;
        Quaternion carryInput, carryOutput;

        void Awake()
        {
            heldItem = GetComponent<HeldItemDisplay>();
            visual = GetComponent<CharacterVisual>();
        }

        public int RigCount => rigs != null ? rigs.Length : 0;
        public int Active => active;
        public Rig GetRig(int index) => rigs[index];
        public Rig ActiveRig => rigs != null && rigs.Length > 0 ? rigs[active] : null;
        /// <summary>The weapon hand of the active body.</summary>
        public Transform Hand => ActiveRig?.hand;
        /// <summary>Set while animation clips pose the skeleton (no procedural bone driving).</summary>
        public bool ClipDriven { get; set; }

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

            var pose = heldItem != null ? heldItem.CurrentGrip : null;
            ApplyFingerPose(rig, pose);

            if (ClipDriven)
            {
                carriedSocket = false;
                Carry(rig, pose);
                if (handSocket != null && rig.hand != null)
                {
                    handSocket.position = rig.hand.TransformPoint(rig.grip);
                    handSocket.rotation = rig.hand.rotation * rig.gripRotation;
                }
                return;
            }

            // The socket is also the procedural wrist's input. Retain the input if a carry pose
            // wrote the last output, so re-evaluating cannot feed the corrected wrist back into itself.
            var socketRotation = handSocket != null ? handSocket.rotation : Quaternion.identity;
            if (handSocket != null)
            {
                if (carriedSocket && Quaternion.Angle(handSocket.localRotation, carryOutput) < 0.001f)
                    socketRotation = (handSocket.parent != null ? handSocket.parent.rotation : Quaternion.identity) * carryInput;
                else carryInput = handSocket.localRotation;
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
                // This pivot controls the item. Pose the wrist using the inverse grip, leaving the
                // pivot's rotation untouched so repeated updates cannot feed rotation back into it.
                var offset = bone == rig.hand && driver == handSocket
                    ? Quaternion.Inverse(rig.gripRotation)
                    : rig.offsets[i];
                bone.rotation = weight >= 0.999f || driver.parent == null
                    ? (driver == handSocket ? socketRotation : driver.rotation) * offset
                    : driver.parent.rotation * Quaternion.Slerp(Quaternion.identity, driver.localRotation, weight) * offset;
            }

            bool carrying = Carry(rig, pose);
            if (handSocket != null && rig.hand != null)
            {
                handSocket.position = rig.hand.TransformPoint(rig.grip);
                handSocket.rotation = carrying ? rig.hand.rotation * rig.gripRotation : socketRotation;
                carryOutput = handSocket.localRotation;
                carriedSocket = carrying;
            }
        }

        bool Carry(Rig rig, ItemHoldPose pose)
        {
            if (pose == null || !pose.carryArm) { carryWeight = 0f; return false; }
            carryWeight = Mathf.MoveTowards(carryWeight, visual != null && visual.CanCarryItem ? 1f : 0f, Time.deltaTime * 12f);
            if (carryWeight <= 0f || rig.hand == null || rig.hand.parent == null || rig.hand.parent.parent == null) return false;
            PoseCarryArm(rig, transform, pose, carryWeight);
            return true;
        }

        /// <summary>A fitted two-bone arm pose; rotations preserve both arm lengths and the hand's grasp.</summary>
        static void PoseCarryArm(Rig rig, Transform character, ItemHoldPose pose, float weight)
        {
            var lower = rig.hand.parent;
            var upper = lower.parent;
            var upperBefore = upper.localRotation;
            var lowerBefore = lower.localRotation;
            var handBefore = rig.hand.localRotation;
            var frame = character.rotation * Quaternion.Euler(pose.carryEulerAngles);
            var wristRotation = frame * Quaternion.Inverse(rig.gripRotation);
            var target = character.TransformPoint(pose.carryPosition) - wristRotation * Vector3.Scale(rig.grip, rig.hand.lossyScale);
            var shoulder = upper.position;
            float a = Vector3.Distance(shoulder, lower.position), b = Vector3.Distance(lower.position, rig.hand.position);
            var delta = target - shoulder;
            float reach = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.0001f, a + b - 0.0001f);
            var direction = delta.normalized;
            var pole = Vector3.ProjectOnPlane(character.TransformPoint(pose.carryElbow) - shoulder, direction).normalized;
            if (pole.sqrMagnitude < 0.001f) pole = Vector3.ProjectOnPlane(lower.position - shoulder, direction).normalized;
            float along = (a * a + reach * reach - b * b) / (2f * reach);
            var elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
            var wristTarget = shoulder + direction * reach;
            lower.rotation = Quaternion.FromToRotation(rig.hand.position - lower.position, wristTarget - lower.position) * lower.rotation;
            rig.hand.rotation = wristRotation;
            var upperAfter = upper.localRotation;
            var lowerAfter = lower.localRotation;
            var handAfter = rig.hand.localRotation;
            upper.localRotation = Quaternion.Slerp(upperBefore, upperAfter, weight);
            lower.localRotation = Quaternion.Slerp(lowerBefore, lowerAfter, weight);
            rig.hand.localRotation = Quaternion.Slerp(handBefore, handAfter, weight);
        }

        /// <summary>The right hand takes the item's fitted grasp; the unoccupied left hand keeps its guard fist.</summary>
        public static void ApplyFingerPose(Rig rig, ItemHoldPose pose)
        {
            if (rig.fingers == null || rig.fist == null) return;
            for (int i = 0; i < rig.fingers.Length && i < rig.fist.Length; i++)
            {
                if (rig.fingers[i] == null) continue;
                bool custom = i >= 15 && pose != null && rig.fingerOpen != null && i < rig.fingerOpen.Length;
                rig.fingers[i].localRotation = custom
                    ? Quaternion.Slerp(rig.fingerOpen[i], rig.fist[i], pose.Curl((i - 15) / 3, (i - 15) % 3))
                    : rig.fist[i];
            }
            if (pose != null && rig.hand != null && rig.fingerOpen != null && rig.fingerOpen.Length >= 18 &&
                rig.fingers.Length >= 18 && rig.fingers[15] != null)
            {
                var frame = rig.hand.rotation * rig.gripRotation;
                rig.fingers[15].rotation = frame * Quaternion.Euler(pose.thumbRotation) * Quaternion.Inverse(frame) * rig.fingers[15].rotation;
            }
        }
    }
}
