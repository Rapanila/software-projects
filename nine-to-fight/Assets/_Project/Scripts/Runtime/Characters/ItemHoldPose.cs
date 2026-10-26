using System;
using UnityEngine;

namespace NineToFight.Characters
{
    /// <summary>A fitted item transform and a separate curl for every joint of the holding hand.</summary>
    [Serializable]
    public sealed class ItemHoldPose
    {
        public Vector3 position;
        public Vector3 eulerAngles;
        [Tooltip("Thumb opposition in the hand's grip frame, after joint curls.")]
        public Vector3 thumbRotation;
        [Tooltip("Proximal, intermediate and distal joint curls: 0 is the original T-pose; 1 is a fist.")]
        public Vector3 thumb = Vector3.one;
        public Vector3 index = Vector3.one;
        public Vector3 middle = Vector3.one;
        public Vector3 ring = Vector3.one;
        public Vector3 pinky = Vector3.one;
        [Tooltip("Pose the holding arm between actions (for items such as an upright cup).")]
        public bool carryArm;
        [Tooltip("Grip position, elbow guide and grip orientation in the character visual's space.")]
        public Vector3 carryPosition, carryElbow, carryEulerAngles;

        public float Curl(int finger, int joint)
        {
            var curls = finger switch
            {
                0 => thumb, 1 => index, 2 => middle, 3 => ring, _ => pinky,
            };
            return Mathf.Clamp01(curls[joint]);
        }
    }
}
