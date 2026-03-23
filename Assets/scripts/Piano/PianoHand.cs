using System.Collections.Generic;
using System.Linq;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;
using Hand;

namespace Piano
{
    /// <summary>
    /// Piano-specific hand behavior that constrains the palm to face down
    /// and uses 0-1 curl values for fingers instead of full rotation tracking.
    /// Add this component alongside a Hand component to override its behavior.
    /// </summary>
    [RequireComponent(typeof(Hand.Hand))]
    public class PianoHand : MonoBehaviour
    {
        // Reference to the Hand component we're interfacing with
        private Hand.Hand _hand;

        private Vector3[] _landmarks = new Vector3[21];
        private Dictionary<Finger, Quaternion[]> _flatHandRotations;
        private Dictionary<Finger, Quaternion[]> _calibrationOffset;
        private Quaternion _flatWristRotation;
        private Quaternion _wristCalibrationOffset = Quaternion.identity;
        private bool _calibrated;

        private void Awake()
        {
            _hand = GetComponent<Hand.Hand>();
            
            // Point to piano and palm to keys
            var facePiano = Vector3.Cross(Vector3.down, _hand._leftHandMultiplier * Vector3.forward);
            _hand.wrist.rotation = Quaternion.LookRotation(facePiano, Vector3.down);
            _flatWristRotation = _hand.wrist.rotation;
            
            // Save flat hand rotations
            _flatHandRotations = new Dictionary<Finger, Quaternion[]>
            {
                { _hand.thumb, GetJointRotations(_hand.thumb) },
                { _hand.index, GetJointRotations(_hand.index) },
                { _hand.middle, GetJointRotations(_hand.middle) },
                { _hand.ring, GetJointRotations(_hand.ring) },
                { _hand.pinky, GetJointRotations(_hand.pinky) }
            };
            
            _calibrationOffset = new Dictionary<Finger, Quaternion[]>
            {
                { _hand.thumb, Enumerable.Repeat(Quaternion.identity, _hand.thumb.joints.Length).ToArray() },
                { _hand.index, Enumerable.Repeat(Quaternion.identity, _hand.index.joints.Length).ToArray() },
                { _hand.middle, Enumerable.Repeat(Quaternion.identity, _hand.middle.joints.Length).ToArray() },
                { _hand.ring, Enumerable.Repeat(Quaternion.identity, _hand.ring.joints.Length).ToArray() },
                { _hand.pinky, Enumerable.Repeat(Quaternion.identity, _hand.pinky.joints.Length).ToArray() }
            };
        }

        private Quaternion[] GetJointRotations(Finger finger)
        {
            var rotations = new Quaternion[finger.joints.Length];
            for (int i = 0; i < finger.joints.Length; i++)
                rotations[i] = finger.joints[i].localRotation;
            return rotations;
        }

        private Quaternion GetWristRotationFromLandmarks()
        {
            var centroid = (_landmarks[0] + _landmarks[1] + _landmarks[5] + _landmarks[9] + _landmarks[13] + _landmarks[17]) / 6;
            var centroidDirection = (centroid - _landmarks[0]).normalized;
            var palmNormal = _hand._leftHandMultiplier * Vector3.Cross(_landmarks[9] - _landmarks[1], _landmarks[17] - _landmarks[9]).normalized;
            return Quaternion.LookRotation(centroidDirection, palmNormal) * _hand.axisCorrection;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Calibrate();
        }

        private void Calibrate()
        {
            // Wrist calibration
            var currentWrist = GetWristRotationFromLandmarks();
            _wristCalibrationOffset = Quaternion.Inverse(currentWrist) * _flatWristRotation;

            // Finger calibration
            foreach (var finger in new[] { _hand.thumb, _hand.index, _hand.middle, _hand.ring, _hand.pinky })
            {
                for (int i = 0; i < finger.joints.Length; i++)
                {
                    var current = finger.joints[i].localRotation;
                    var flat = _flatHandRotations[finger][i];
                    _calibrationOffset[finger][i] = Quaternion.Inverse(current) * flat;
                }
            }
            _calibrated = true;
            Debug.Log("Hand calibrated");
        }

        public void UpdateFromLandmarks(NormalizedLandmarks landmarks)
        {
            if (!_hand) return;
            if (!_calibrated) { _hand.UpdateFromLandmarks(landmarks); return; }
            if (landmarks.landmarks == null || landmarks.landmarks.Count < 21) return;

            // Convert landmarks to Unity space
            for (int i = 0; i < 21; i++) _landmarks[i] = Handy.ToVector3(landmarks.landmarks[i]);

            // Update wrist
            _hand.wrist.rotation = GetWristRotationFromLandmarks() * _wristCalibrationOffset;

            UpdateFinger(_hand.thumb, true);
            UpdateFinger(_hand.index);
            UpdateFinger(_hand.middle);
            UpdateFinger(_hand.ring);
            UpdateFinger(_hand.pinky);
        }
        
        private void UpdateFinger(Finger chain, bool isThumb = false)
        {
            if (chain.joints == null || chain.joints.Length < 2) return;

            // Meta bone - point toward first finger landmark
            if (!isThumb)
            {
                var wristToFingerVector = (_landmarks[chain.startLandmark] - _landmarks[0]).normalized
                                          * Vector3.Distance(_hand.wrist.position, chain.joints[1].position);
                var metaToFingerVector = wristToFingerVector - (chain.joints[0].position - _hand.wrist.position);
                chain.joints[0].rotation = Quaternion.LookRotation(metaToFingerVector, chain.joints[0].up)
                                           * _hand.axisCorrection * _calibrationOffset[chain][0];
            }

            // Remaining joints - point at next landmark
            int landmarkIndex = chain.startLandmark;
            for (int i = 1; i < chain.joints.Length; i++)
            {
                var direction = _landmarks[landmarkIndex + 1] - _landmarks[landmarkIndex];
                var rotation = Quaternion.LookRotation(direction, chain.joints[i].parent.up)
                                * _hand.axisCorrection * _calibrationOffset[chain][i];
                chain.joints[i].rotation = rotation;
                landmarkIndex++;
            }
        }
    }
}
