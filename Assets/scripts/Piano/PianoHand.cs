using System.Collections.Generic;
using UnityEngine;
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

        // Flat hand reference rotations (piano hand at rest)
        private Dictionary<Finger, Quaternion[]> _flatHandRotations;
        private Quaternion _flatWristRotation;

        // Calibration state - wrist
        private Vector3 _restingWristPosition;
        private Vector3 _pianoRestingPosition;
        private Quaternion _restingWristRotation;

        // Calibration state - fingers (free hand joint rotations at calibration)
        private Dictionary<Finger, Quaternion[]> _restingFingerRotations;

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

            // Apply finger rotation deltas
            ApplyFingerRotations(_hand.thumb, freeHand.thumb);
            ApplyFingerRotations(_hand.index, freeHand.index);
            ApplyFingerRotations(_hand.middle, freeHand.middle);
            ApplyFingerRotations(_hand.ring, freeHand.ring);
            ApplyFingerRotations(_hand.pinky, freeHand.pinky);
        }

        /// <summary>
        /// Apply rotation delta from free hand finger to piano hand finger.
        /// </summary>
        private void ApplyFingerRotations(Finger pianoFinger, Finger freeFinger)
        {
            if (pianoFinger.joints == null || freeFinger.joints == null) return;
            if (!_flatHandRotations.TryGetValue(pianoFinger, out var flatRotations)) return;
            if (!_restingFingerRotations.TryGetValue(pianoFinger, out var restingRotations)) return;

            int count = Mathf.Min(pianoFinger.joints.Length, freeFinger.joints.Length);
            for (int i = 0; i < count; i++)
            {
                // Rotation delta: current free hand rotation relative to calibrated resting
                Quaternion rotDelta = freeFinger.joints[i].localRotation * Quaternion.Inverse(restingRotations[i]);
                // Apply delta to piano hand's flat rotation
                pianoFinger.joints[i].localRotation = rotDelta * flatRotations[i];
            }
        }

        /// <summary>
        /// Calibrate this piano hand. Captures the free hand's current position and finger rotations as "resting".
        /// </summary>
        public void Calibrate()
        {
            if (!freeHand)
            {
                Debug.LogWarning("PianoHand: Cannot calibrate without free hand reference");
                return;
            }

            // Wrist calibration
            _restingWristPosition = freeHand.wrist.position;
            _restingWristRotation = freeHand.wrist.rotation;
            _pianoRestingPosition = _hand.wrist.position;

            // Finger calibration - capture free hand joint rotations
            _restingFingerRotations = new Dictionary<Finger, Quaternion[]>
            {
                { _hand.thumb, GetJointRotations(freeHand.thumb) },
                { _hand.index, GetJointRotations(freeHand.index) },
                { _hand.middle, GetJointRotations(freeHand.middle) },
                { _hand.ring, GetJointRotations(freeHand.ring) },
                { _hand.pinky, GetJointRotations(freeHand.pinky) }
            };

            _calibrated = true;
            Debug.Log($"PianoHand calibrated: resting pos = {_restingWristPosition}");
        }
    }
}
