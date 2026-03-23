using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

namespace Hand
{
    public class Handy
    {
        private Dictionary<string, float[]> _spineLengths = new();
        public float scaleCentroid;
        private bool _isCached;

        public void CacheFinger(Finger finger, Vector3 origin)
        {
            // _spineLengths = spineLengths;
            Transform[] joints = finger.landmarkBase.GetComponentsInChildren<Transform>();
            if (joints == null || joints.Length == 0) { Debug.Log($"{finger.name} has no joints"); return; }
            
            float[] lengths = new float[joints.Length];
            
            lengths[0] = Vector3.Distance(origin, joints[0].position);
            
            for (int i = 1; i < joints.Length; i++)
            {
                lengths[i] = Vector3.Distance(joints[i - 1].position, joints[i].position);
            }
            _spineLengths[finger.name] = lengths;
            Debug.Log($"Successfully Cached {finger.name} with {lengths.Length} lengths");
        }

        public void CacheComplete()
        {
            float sum = 0f;
            foreach (var lengths in _spineLengths.Values)
            {
                sum += lengths[0];
            }
            scaleCentroid = sum / _spineLengths.Count;
            _isCached = true;
        }

        public bool isCached()
        {
            return _isCached;
        }

        public void DrawSpine(Finger finger, Vector3[] landmarks, Vector3 origin, Color? color = null, float dotSize=0.003f)
        {
            if (!_spineLengths.TryGetValue(finger.name, out var lengths)) return;

            Gizmos.color = color ?? Color.white;
            var currentPosition = origin;
            
            Vector3 toFirst = (landmarks[finger.startLandmark] - landmarks[0]).normalized;
            Gizmos.DrawLine(currentPosition, currentPosition + toFirst * lengths[0]);
            currentPosition += toFirst * lengths[0];
            Gizmos.DrawSphere(currentPosition, dotSize);

            // Draw remaining segments
            var landmarkIndex = finger.startLandmark;
            for (int i = 0; i < lengths.Length - 1; i++)
            {
                Vector3 direction = (landmarks[landmarkIndex + 1] - landmarks[landmarkIndex]).normalized;
                float length = lengths[i + 1];

                Gizmos.DrawLine(currentPosition, currentPosition + direction * length);
                currentPosition += direction * length;
                Gizmos.DrawSphere(currentPosition, dotSize);
                landmarkIndex++;
            }
        }
        
        public static Vector3 ToVector3(NormalizedLandmark lm, float xSign = -1f) => new (xSign * lm.x, lm.y, -lm.z);
        
        // public static void UpdateChain(Vector3[] landmarks, FingerChain chain, Vector3? metaNormal = null)
        // {
        //     if (chain.joints == null) return;
        //     // TODO: Assert check on awake that this will work joints 1 less then indices
        //     
        //     for (int i = 0; i < chain.joints.Length; i++)
        //     {
        //         Vector3 direction = landmarks[chain[i + 1]] - landmarks[chain.landmarkIndices[i]];
        //         if (!(direction.sqrMagnitude > 0.0001f)) continue;
        //         direction.Normalize();
        //         Vector3 _normal;
        //         if (metaNormal != null)
        //         {
        //             _normal = (Vector3)(i == 0 ? metaNormal : chain.joints[i].parent.up);
        //         } else {
        //             _normal = chain.joints[i].parent.up;
        //         }
        //
        //         chain.joints[i].rotation = Quaternion.LookRotation(direction, _normal);
        //     }
        // }
        
        public static void SetJointRotation(Transform joint, Vector3 direction, Vector3 normal)
        {
            joint.rotation = Quaternion.LookRotation(direction, normal); // * Quaternion.Euler(0, 90, 0)
        }
    }
}