using UnityEngine;

namespace Gestures
{
    [System.Serializable]
    public class GestureStep
    {
        [Tooltip("Pose for left hand (leave empty if not required)")]
        public HandPose leftPose;
        [Tooltip("Pose for right hand (leave empty if not required)")]
        public HandPose rightPose;
    }

    /// <summary>
    /// Maps gesture pose(s) to an action command.
    /// Can be a single pose or a sequence of poses.
    /// </summary>
    [CreateAssetMenu(fileName = "GestureAction", menuName = "Gestures/Action")]
    public class GestureAction : ScriptableObject
    {
        public string actionName;

        [Header("Gesture Steps")]
        [Tooltip("Sequence of gesture steps. Single step = instant action, multiple = sequence.")]
        public GestureStep[] steps;

        [Header("Action")]
        public GestureCommand commandToTrigger;

        [Header("Cooldown")]
        [Tooltip("Minimum time between action triggers")]
        public float cooldownSeconds = 0.5f;

        public int StepCount => steps?.Length ?? 0;
        public bool IsSequence => StepCount > 1;

        /// <summary>
        /// Get the pose for a specific hand at a specific step.
        /// </summary>
        public HandPose GetPose(int stepIndex, bool isLeftHand)
        {
            if (steps == null || stepIndex < 0 || stepIndex >= steps.Length) return null;
            return isLeftHand ? steps[stepIndex].leftPose : steps[stepIndex].rightPose;
        }

        /// <summary>
        /// Return what Hands are Required (left (bool), right (bool)) 
        /// </summary>
        public (bool,bool) HandsRequired(int stepIndex)
        {
            return (GetPose(stepIndex, false), GetPose(stepIndex, true));
        }
    }
}