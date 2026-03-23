using UnityEngine;

namespace Gestures
{
    /// <summary>
    /// Reference pose captured by developer in editor.
    /// Stores metadata for comparison during gesture detection.
    /// </summary>
    [CreateAssetMenu(fileName = "HandPose", menuName = "Gestures/Hand Pose")]
    public class HandPose : ScriptableObject
    {
        public string poseName;

        [Header("Hand Side")]
        public bool isLeftHand;

        [Header("Reference Metadata")]
        public HandMetadata referenceMetadata;

        [Header("Matching Settings")]
        [Tooltip("Use fingertip position matching")]
        public bool useEuclideanSimilarity;
        [Tooltip("Maximum fingertip deviation")]
        public float maxFingertipDistance = 0.3f;
        [Tooltip("Use finger curl matching")]
        public bool useCurlSimilarity;
        [Tooltip("Use palm normal matching")]
        public bool usePalmSimilarity;
        [Tooltip("Require wrist to be near a specific vertical angle (0=horizontal)")]
        public bool useWristVerticalSimilarity;
        [Tooltip("Max deviation from reference wristVerticalDot")]
        [Range(0f, 1f)] public float wristVerticalTolerance = 0.3f;
        [Range(0f, 1f)] public float similarityThreshold = 0.85f;


        /// <summary>
        /// Check fingertip positions similarity using Euclidean distance.
        /// </summary>
        public bool GetEuclideanSimilarity(HandMetadata current)
        {
            float thumbDist = Vector3.Distance(current.thumbTip, referenceMetadata.thumbTip);
            float indexDist = Vector3.Distance(current.indexTip, referenceMetadata.indexTip);
            float middleDist = Vector3.Distance(current.middleTip, referenceMetadata.middleTip);
            float ringDist = Vector3.Distance(current.ringTip, referenceMetadata.ringTip);
            float pinkyDist = Vector3.Distance(current.pinkyTip, referenceMetadata.pinkyTip);

            float avgDist = (thumbDist + indexDist + middleDist + ringDist + pinkyDist) / 5f;
            float similarity = Mathf.Clamp01(1f - (avgDist / maxFingertipDistance));
            return similarity >= similarityThreshold;
        }

        /// <summary>
        /// Check finger curl similarity by comparing curl values.
        /// </summary>
        public bool GetCurlSimilarity(HandMetadata current)
        {
            float thumbDiff = Mathf.Abs(current.thumbCurl - referenceMetadata.thumbCurl);
            float indexDiff = Mathf.Abs(current.indexCurl - referenceMetadata.indexCurl);
            float middleDiff = Mathf.Abs(current.middleCurl - referenceMetadata.middleCurl);
            float ringDiff = Mathf.Abs(current.ringCurl - referenceMetadata.ringCurl);
            float pinkyDiff = Mathf.Abs(current.pinkyCurl - referenceMetadata.pinkyCurl);

            float avgDiff = (thumbDiff + indexDiff + middleDiff + ringDiff + pinkyDiff) / 5f;
            float similarity = Mathf.Clamp01(1f - avgDiff);
            return similarity >= similarityThreshold;
        }

        /// <summary>
        /// Check palm normal similarity using cosine similarity.
        /// </summary>
        public bool GetPalmSimilarity(HandMetadata current)
        {
            // Dot product of normalized vectors: -1 to 1 -> convert to 0 to 1
            float dot = Vector3.Dot(current.palmNormal.normalized, referenceMetadata.palmNormal.normalized);
            float similarity = (dot + 1f) / 2f;
            return similarity >= similarityThreshold;
        }

        /// <summary>
        /// Check wrist vertical orientation similarity.
        /// Ensures wrist is near the reference vertical angle (e.g., 0 = horizontal).
        /// </summary>
        public bool GetWristVerticalSimilarity(HandMetadata current)
        {
            return Mathf.Abs(current.wristVerticalDot - referenceMetadata.wristVerticalDot) <= wristVerticalTolerance;
        }

        /// <summary>
        /// Check if current metadata matches this pose.
        /// </summary>
        public bool Matches(HandMetadata current)
        {
            if (useEuclideanSimilarity && !GetEuclideanSimilarity(current)) return false;
            if (useCurlSimilarity && !GetCurlSimilarity(current)) return false;
            if (usePalmSimilarity && !GetPalmSimilarity(current)) return false;
            if (useWristVerticalSimilarity && !GetWristVerticalSimilarity(current)) return false;
            return true;
        }

    }
}