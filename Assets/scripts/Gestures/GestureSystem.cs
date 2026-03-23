using System.Collections.Generic;
using UnityEngine;

namespace Gestures
{
    public enum GestureCommand
    {
        None = 0,
        Calibrate = 1,
        OctaveUp = 2,
        OctaveDown = 3
    }
    
    /// <summary>
    /// Central orchestrator for gesture detection and sequence handling.
    /// Reads metadata from Hand components and evaluates action configs.
    /// </summary>
    public class GestureSystem : MonoBehaviour
    {
        [Header("Hand References")]
        [Tooltip("Reference to PianoHandLandmarker to get free hands at runtime")]
        public Piano.PianoHandLandmarker handLandmarker;

        [Header("Actions")]
        public GestureAction[] actions;

        [Header("Global Sequence Settings")]
        [Tooltip("Maximum time between sequence steps before reset")]
        public float maxStepInterval = 1.5f;
        [Tooltip("Minimum time to hold each pose")]
        public float minHoldDuration = 0.2f;

        [Header("Debug")]
        public bool debugLogging;

        private Dictionary<GestureAction, ActionState> _states;

        /// <summary>
        /// Runtime state for tracking an action's sequence progress.
        /// </summary>
        private class ActionState
        {
            public int currentStep;
            public float holdStartTime;
            public float waitStartTime; // wait is used for sequences
            public float sequenceStartTime;
            public bool holdRequirementMet;
            public float lastTriggerTime;

            public void Reset()
            {
                currentStep = 0;
                holdStartTime = 0f;
                waitStartTime = 0f;
                sequenceStartTime = 0f;
                holdRequirementMet = false;
            }
        }

        private void Awake()
        {
            _states = new Dictionary<GestureAction, ActionState>();

            if (actions != null)
            {
                foreach (var action in actions)
                {
                    if (action && !_states.ContainsKey(action))
                        _states[action] = new ActionState();
                }
            }
        }

        private void Update()
        {
            if (actions == null || handLandmarker?.LeftFreeHand?.Metadata == null 
                                || handLandmarker?.RightFreeHand?.Metadata == null) 
                return;
            
            var leftMeta = handLandmarker.LeftFreeHand.Metadata;
            var rightMeta = handLandmarker.RightFreeHand.Metadata;
            
            foreach (var action in actions)
            {
                if (!action || !_states.TryGetValue(action, out var state) || action.StepCount == 0) continue;
                if (Time.time - state.lastTriggerTime < action.cooldownSeconds) return;
                
                int stepIndex = action.IsSequence ? state.currentStep : 0;
                if (stepIndex >= action.StepCount) return;
                
                bool conditionMet = StepMatches(action, stepIndex, leftMeta, rightMeta);
                if (action.IsSequence) HandleSequenceStep(action, state, conditionMet);
                else HandleSinglePose(action, state, conditionMet);
            }
        }

        /// <summary>
        /// Checks whether the required hand pose(s) for a specific step match current metadata.
        /// </summary>
        private static bool StepMatches(GestureAction action, int stepIndex, HandMetadata leftMeta, HandMetadata rightMeta)
        {
            var (requiresLeft, requiresRight) = action.HandsRequired(stepIndex);

            // Invalid step: neither hand specified
            if (!requiresLeft && !requiresRight) return false;

            var (leftMatches, rightMatches) = (true, true);
            if (requiresLeft) 
                leftMatches = action.GetPose(stepIndex, true)?.Matches(leftMeta) ?? false;
            if (requiresRight) 
                rightMatches = action.GetPose(stepIndex, false)?.Matches(rightMeta) ?? false;

            return leftMatches && rightMatches;
        }

        private void HandleSinglePose(
            GestureAction action,
            ActionState state,
            bool conditionMet)
        {
            if (!conditionMet)
            {
                state.holdRequirementMet = false;
                state.holdStartTime = 0f;
                return;
            }

            if (state.holdStartTime == 0f)
                state.holdStartTime = Time.time;

            if (Time.time - state.holdStartTime >= minHoldDuration)
            {
                if (!state.holdRequirementMet)
                {
                    state.holdRequirementMet = true;
                    TriggerAction(action, state);
                }
            }
        }

        private void HandleSequenceStep(
            GestureAction action,
            ActionState state,
            bool conditionMet)
        {
            switch (state.currentStep)
            {
                // Timeout between steps
                case 0 when state.sequenceStartTime == 0f:
                    state.sequenceStartTime = Time.time;
                    break;
                case > 0 when state.waitStartTime > 0f && Time.time - state.waitStartTime > maxStepInterval:
                {
                    if (debugLogging) Debug.Log($"GestureSystem: Sequence '{action.actionName}' timed out at step {state.currentStep}");

                    state.Reset();
                    return;
                }
            }

            if (!conditionMet)
            {
                // Advance only after current step was held and then released/changed
                if (state.holdRequirementMet && state.currentStep < action.StepCount - 1)
                {
                    state.currentStep++;
                    state.holdRequirementMet = false;
                    state.holdStartTime = 0f;
                    state.waitStartTime = Time.time;

                    if (debugLogging) Debug.Log($"GestureSystem: Sequence '{action.actionName}' advanced to step {state.currentStep}");
                } else {
                    state.holdStartTime = 0f; // Lost match before satisfying hold time
                }
                return;
            }

            // Current step is matching
            if (state.holdStartTime == 0f) state.holdStartTime = Time.time;
            state.waitStartTime = 0f; // stop wait timeout
            
            if (Time.time - state.holdStartTime >= minHoldDuration)
            {
                state.holdRequirementMet = true;
                if (state.currentStep < action.StepCount - 1) return;
                
                TriggerAction(action, state);
                state.Reset();
            }
        }

        private void TriggerAction(GestureAction action, ActionState state)
        {
            state.lastTriggerTime = Time.time;

            if (debugLogging)
                Debug.Log($"GestureSystem: Triggered action '{action.actionName}' (Command: {action.commandToTrigger})");

            ExecuteCommand(action.commandToTrigger);
        }

        private void ExecuteCommand(GestureCommand command)
        {
            switch (command)
            {
                case GestureCommand.Calibrate:
                    handLandmarker?.CalibratePianoHands();
                    break;

                case GestureCommand.OctaveUp:
                    Debug.Log("GestureSystem: OctaveUp command triggered");
                    // TODO: Implement octave up logic
                    break;

                case GestureCommand.OctaveDown:
                    Debug.Log("GestureSystem: OctaveDown command triggered");
                    // TODO: Implement octave down logic
                    break;

                case GestureCommand.None:
                default:
                    break;
            }
        }
    }
}
