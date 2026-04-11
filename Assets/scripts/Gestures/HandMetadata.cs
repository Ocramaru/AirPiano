using UnityEngine;

namespace Gestures
{
    /// <summary>
    /// Per-frame hand state computed from Hand model after UpdateFromLandmarks.
    /// Used for gesture similarity matching.
    /// </summary>
    [System.Serializable]
    public struct HandMetadata
    {
        public bool isValid;

        // Per-finger curl (0=extended, 1=curled) - from local joint rotations
        public float thumbCurl;
        public float indexCurl;
        public float middleCurl;
        public float ringCurl;
        public float pinkyCurl;

        // Palm orientation
        public Vector3 palmNormal;

        // Fingertip positions (local to wrist, for similarity matching)
        public Vector3 thumbTip;
        public Vector3 indexTip;
        public Vector3 middleTip;
        public Vector3 ringTip;
        public Vector3 pinkyTip;

        // Wrist state
        public Quaternion wristRotation;

        // Vertical tilt: +1 = wrist up facing up, -1 = facing down, 0 = horizontal
        // Invariant to horizontal wrist rotation
        public float wristVerticalDot;

        // Computed helpers
        public float AverageCurl => (thumbCurl + indexCurl + middleCurl + ringCurl + pinkyCurl) / 5f;

        /// <summary>
        /// Check if palm is flipped relative to a resting normal (e.g., palm facing up vs down).
        /// </summary>
        public bool IsPalmFlipped(Vector3 restingNormal) => Vector3.Angle(palmNormal, restingNormal) > 90f;

        /// <summary>
        /// Get all curl values as an array for iteration.
        /// </summary>
        public float[] GetCurlArray() => new[] { thumbCurl, indexCurl, middleCurl, ringCurl, pinkyCurl };

        /// <summary>
        /// Get all fingertip positions as an array for iteration.
        /// </summary>
        public Vector3[] GetFingertipArray() => new[] { thumbTip, indexTip, middleTip, ringTip, pinkyTip };
    }
}