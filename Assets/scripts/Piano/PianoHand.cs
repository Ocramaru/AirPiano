using System.Collections.Generic;
using UnityEngine;
using Gestures;
using Hand;

namespace Piano
{
    /// <summary>
    /// Piano-specific hand behavior driven by a Free Hand reference.
    /// Position is mapped via calibration offset: (FreeHand.position - restingPos) + pianoRestingPos
    /// Finger curls are copied from the free hand's metadata.
    /// </summary>
    [RequireComponent(typeof(Hand.Hand))]
    public class PianoHand : MonoBehaviour
    {
        [Header("Free Hand Reference")]
        [Tooltip("The free hand this piano hand follows")]
        public Hand.Hand freeHand;

        private Hand.Hand _hand;

        // Flat hand reference rotations
        private Dictionary<Finger, Quaternion[]> _flatHandRotations;
        private Quaternion _flatWristRotation;

        // Calibration state
        private Vector3 _restingWristPosition;
        private Vector3 _pianoRestingPosition;
        private Quaternion _restingWristRotation;
        private bool _calibrated;
        public bool IsCalibrated => _calibrated;

        private void Awake()
        {
            _hand = GetComponent<Hand.Hand>();

            // Point to piano and palm to keys
            var facePiano = Vector3.Cross(_hand._leftHandMultiplier * Vector3.forward, Vector3.down);
            _hand.wrist.rotation = Quaternion.LookRotation(facePiano, Vector3.down);
            _flatWristRotation = _hand.wrist.rotation;

            // Save flat hand rotations for finger curl application
            _flatHandRotations = new Dictionary<Finger, Quaternion[]>
            {
                { _hand.thumb, GetJointRotations(_hand.thumb) },
                { _hand.index, GetJointRotations(_hand.index) },
                { _hand.middle, GetJointRotations(_hand.middle) },
                { _hand.ring, GetJointRotations(_hand.ring) },
                { _hand.pinky, GetJointRotations(_hand.pinky) }
            };
        }

        private Quaternion[] GetJointRotations(Finger finger)
        {
            var rotations = new Quaternion[finger.joints.Length];
            for (int i = 0; i < finger.joints.Length; i++)
                rotations[i] = finger.joints[i].localRotation;
            return rotations;
        }

        private void LateUpdate()
        {
            if (!freeHand || !_calibrated) return;

            // Position: offset from calibrated resting
            Vector3 delta = freeHand.wrist.position - _restingWristPosition;
            _hand.wrist.position = _pianoRestingPosition + delta;

            // Rotation: apply relative rotation from resting
            Quaternion rotDelta = freeHand.wrist.rotation * Quaternion.Inverse(_restingWristRotation);
            _hand.wrist.rotation = rotDelta * _flatWristRotation;

            // Copy finger curls from free hand metadata
            ApplyFingerCurlsFromMetadata(freeHand.Metadata);
        }

        /// <summary>
        /// Calibrate this piano hand. Captures the free hand's current position as "resting".
        /// </summary>
        public void Calibrate()
        {
            if (!freeHand)
            {
                Debug.LogWarning("PianoHand: Cannot calibrate without free hand reference");
                return;
            }

            _restingWristPosition = freeHand.wrist.position;
            _restingWristRotation = freeHand.wrist.rotation;
            _pianoRestingPosition = _hand.wrist.position;
            _calibrated = true;
            Debug.Log($"PianoHand calibrated: resting pos = {_restingWristPosition}");
        }

        private void ApplyFingerCurlsFromMetadata(HandMetadata metadata)
        {
            ApplyFingerCurl(_hand.thumb, metadata.thumbCurl, true);
            ApplyFingerCurl(_hand.index, metadata.indexCurl);
            ApplyFingerCurl(_hand.middle, metadata.middleCurl);
            ApplyFingerCurl(_hand.ring, metadata.ringCurl);
            ApplyFingerCurl(_hand.pinky, metadata.pinkyCurl);
        }

        private void ApplyFingerCurl(Finger finger, float curl, bool isThumb = false)
        {
            if (finger.joints == null || finger.joints.Length < 2) return;
            if (!_flatHandRotations.TryGetValue(finger, out var flatRotations)) return;

            for (int i = isThumb ? 0 : 1; i < finger.joints.Length; i++)
            {
                float curlAngle = curl * 90f;
                var curledRotation = flatRotations[i] * Quaternion.Euler(curlAngle, 0f, 0f);
                finger.joints[i].localRotation = curledRotation;
            }
        }
    }
}
