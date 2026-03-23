using UnityEngine;
using Mediapipe.Tasks.Components.Containers;
using Gestures;

namespace Hand
{

    public class Hand : MonoBehaviour
    {
        [Header("Configuration")]
        public bool isLeftHand;

        [Header("Joints")]
        public Transform wrist;

        [Header("Finger Chains")]
        public Finger thumb = new Finger { name = "Thumb", startLandmark = 1 };
        public Finger index = new Finger { name = "Index", startLandmark = 5 };
        public Finger middle = new Finger { name = "Middle", startLandmark = 9 };
        public Finger ring = new Finger { name = "Ring", startLandmark = 13 };
        public Finger pinky = new Finger { name = "Pinky", startLandmark = 17 };

        private Vector3[] _landmarks = new Vector3[21];
        private Vector3 _palmNormal;

        // Cached bone lengths per chain: [wrist->_a, _a->_b, _b->_c, _c->_end]
        public bool drawDebugSpines;
        private Handy handy;

        /// <summary>
        /// Per-frame metadata computed after UpdateFromLandmarks.
        /// Contains finger curls, palm normal, and fingertip positions for gesture matching.
        /// </summary>
        public HandMetadata Metadata { get; private set; }

        // Model uses -X and X as forward right and left, LookRotation uses Z as forward
        public Quaternion axisCorrection;
        public float _leftHandMultiplier;

        private void OnValidate() {
            // Right hand: -X toward fingertips, Left hand: +X toward fingertips
            axisCorrection = isLeftHand ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(0, -90, 0);
            _leftHandMultiplier = isLeftHand ? -1f : 1f;
        }

        private void Awake()
        {
            axisCorrection = isLeftHand ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(0, -90, 0);
            _leftHandMultiplier = isLeftHand ? -1f : 1f;
            handy = new Handy();
            if (!drawDebugSpines) return;
            CacheSpineLengths();
        }

        private void CacheSpineLengths()
        {
            // Debug.Log("Caching spine lengths");
            handy.CacheFinger(thumb, wrist.position);
            handy.CacheFinger(index, wrist.position);
            handy.CacheFinger(middle, wrist.position);
            handy.CacheFinger(ring, wrist.position);
            handy.CacheFinger(pinky, wrist.position);
            handy.CacheComplete();
        }

        public void UpdateFromLandmarks(NormalizedLandmarks landmarks)
        {
            if (landmarks.landmarks == null || landmarks.landmarks.Count < 21) return;

            // Convert Landmarks to Unity Space
            for (int i = 0; i < 21; i++) _landmarks[i] = Handy.ToVector3(landmarks.landmarks[i]);
            
            // Centroid (basically palm)
            Vector3 centroid = (_landmarks[0] + _landmarks[1] + _landmarks[5] + _landmarks[9] + _landmarks[13] + _landmarks[17]) / 6;
            Vector3 centroidDirection = (centroid - _landmarks[0]).normalized;
            _palmNormal = _leftHandMultiplier * Vector3.Cross(_landmarks[17] - _landmarks[9], _landmarks[9] - _landmarks[1]).normalized;

            // Wrist points to centroid of palm
            wrist.rotation = Quaternion.LookRotation(centroidDirection, _palmNormal) * axisCorrection;

            // Fingers
            UpdateFinger(thumb, true);
            UpdateFinger(index);
            UpdateFinger(middle);
            UpdateFinger(ring);
            UpdateFinger(pinky);

            // Compute metadata for gesture matching
            ComputeMetadata();
        }

        /// <summary>
        /// Compute finger curl by measuring angle between consecutive bone directions.
        /// Returns 0 for extended, 1 for fully curled.
        /// </summary>
        private float ComputeFingerCurl(Finger finger, bool isThumb = false)
        {
            if (finger.joints == null || finger.joints.Length < 2) return 0f;

            float totalAngle = 0f;

            for (int i = 0; i < finger.joints.Length - 1; i++)
            {
                Vector3 boneDirection = finger.joints[i].right * _leftHandMultiplier;
                Vector3 nextBoneDirection = finger.joints[i + 1].right * _leftHandMultiplier;
                totalAngle += Vector3.Angle(boneDirection, nextBoneDirection);

                // Debug: draw bone directions for each joint
                Color color = i == 0 ? Color.red : (i == 1 ? Color.yellow : (i == 2 ? Color.green : Color.cyan));
                Debug.DrawLine(finger.joints[i].position, finger.joints[i].position + boneDirection * 0.05f, color);
            }

            // Thumb has less range, fingers need slightly higher divisor
            float maxAngle = isThumb ? 90f : 150f;
            return Mathf.Clamp01(totalAngle / maxAngle);
        }

        /// <summary>
        /// Compute metadata after landmark update for gesture matching.
        /// Fingertip positions are in wrist-local space (rotation-invariant).
        /// </summary>
        private void ComputeMetadata()
        {
            // Inverse wrist rotation for transforming to local space
            Quaternion inverseWristRotation = Quaternion.Inverse(wrist.rotation);

            Metadata = new HandMetadata
            {
                thumbCurl = ComputeFingerCurl(thumb, isThumb: true),
                indexCurl = ComputeFingerCurl(index),
                middleCurl = ComputeFingerCurl(middle),
                ringCurl = ComputeFingerCurl(ring),
                pinkyCurl = ComputeFingerCurl(pinky),
                palmNormal = _palmNormal,
                // Fingertip positions in wrist-local space (rotation-invariant)
                thumbTip = thumb.joints != null && thumb.joints.Length > 0
                    ? inverseWristRotation * (thumb.joints[^1].position - wrist.position) : Vector3.zero,
                indexTip = index.joints != null && index.joints.Length > 0
                    ? inverseWristRotation * (index.joints[^1].position - wrist.position) : Vector3.zero,
                middleTip = middle.joints != null && middle.joints.Length > 0
                    ? inverseWristRotation * (middle.joints[^1].position - wrist.position) : Vector3.zero,
                ringTip = ring.joints != null && ring.joints.Length > 0
                    ? inverseWristRotation * (ring.joints[^1].position - wrist.position) : Vector3.zero,
                pinkyTip = pinky.joints != null && pinky.joints.Length > 0
                    ? inverseWristRotation * (pinky.joints[^1].position - wrist.position) : Vector3.zero,
                wristRotation = wrist.rotation,
                wristVerticalDot = Vector3.Dot(wrist.up, Vector3.up)
            };
        }

        private void UpdateFinger(Finger chain, bool isThumb = false)
        {
            if (chain.joints == null || chain.joints.Length < 2) return;
            
            // Meta bone - point toward first finger landmark
            if (!isThumb)
            {
                var wristToFingerVector = (_landmarks[chain.startLandmark] - _landmarks[0]).normalized
                                          * Vector3.Distance(wrist.position, chain.joints[1].position);
                var metaToFingerVector = wristToFingerVector - (chain.joints[0].position - wrist.position);
                chain.joints[0].rotation = Quaternion.LookRotation(metaToFingerVector, chain.joints[0].up) * axisCorrection;
            }

            // Remaining joints - point at next landmark
            int landmarkIndex = chain.startLandmark;
            for (int i = isThumb ? 0 : 1; i < chain.joints.Length; i++)
            {
                chain.joints[i].rotation = Quaternion.LookRotation(
                    _landmarks[landmarkIndex + 1] - _landmarks[landmarkIndex],
                    chain.joints[i].parent.up) * axisCorrection;
                landmarkIndex++;
            }
        }

        public void OnDrawGizmos()
        {
            if (!drawDebugSpines) return;
            if (!wrist || _landmarks == null || _landmarks[0] == Vector3.zero) return;
            if (handy == null) return;
            if (!handy.isCached()) CacheSpineLengths();
            
            // Draw palm normal
            Vector3 centroid = (_landmarks[0] + _landmarks[1] + _landmarks[5] + _landmarks[9] + _landmarks[13] + _landmarks[17]) / 6f;
            Vector3 palmWorldPosition = wrist.position + (centroid - _landmarks[0]) * handy.scaleCentroid;
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(palmWorldPosition, 0.01f);
            Gizmos.DrawLine(palmWorldPosition, palmWorldPosition + _palmNormal * 0.1f);

            // Draw finger chains using cached lengths
            handy.DrawSpine(thumb, _landmarks, wrist.position, Color.red);
            handy.DrawSpine(index, _landmarks, wrist.position, Color.green);
            handy.DrawSpine(middle, _landmarks, wrist.position, Color.blue);
            handy.DrawSpine(ring, _landmarks, wrist.position, Color.cyan);
            handy.DrawSpine(pinky, _landmarks, wrist.position, Color.white);
        }
    }
}
