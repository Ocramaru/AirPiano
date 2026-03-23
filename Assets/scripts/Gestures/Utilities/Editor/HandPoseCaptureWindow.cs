using UnityEngine;
using UnityEditor;
using Piano;

namespace Gestures.Utilities
{
    public class HandPoseCaptureWindow : EditorWindow
    {
        private PianoHandLandmarker _handLandmarker;
        private HandPose _targetPose;
        private bool _useLeftHand = true;
        private bool _useEuclideanSimilarity = true;
        private float _maxFingertipDistance = 0.1f;
        private bool _useCurlSimilarity = true;
        private bool _usePalmSimilarity = true;
        private bool _useWristVerticalSimilarity;
        private float _wristVerticalTolerance = 0.3f;
        private float _similarityThreshold = 0.85f;
        private Vector2 _scrollPos;

        [MenuItem("Window/Gestures/Hand Pose Capture")]
        public static void ShowWindow()
        {
            GetWindow<HandPoseCaptureWindow>("Pose Capture");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Hand Pose Capture", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to capture poses.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            // Find hand landmarker if not set
            if (!_handLandmarker)
            {
                _handLandmarker = FindFirstObjectByType<PianoHandLandmarker>();
            }

            _handLandmarker = (PianoHandLandmarker)EditorGUILayout.ObjectField(
                "Hand Landmarker", _handLandmarker, typeof(PianoHandLandmarker), true);

            if (!_handLandmarker)
            {
                EditorGUILayout.HelpBox("No PianoHandLandmarker found in scene.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }
            
            EditorGUILayout.Space();

            _targetPose = (HandPose)EditorGUILayout.ObjectField(
                "Target Pose Asset", _targetPose, typeof(HandPose), false);
            _useLeftHand = EditorGUILayout.Toggle("Capture From Left Hand", _useLeftHand);
            EditorGUILayout.Space();
            
            // Specify threshold and similarity types
            EditorGUILayout.LabelField("Similarity Options", EditorStyles.boldLabel);
            _useEuclideanSimilarity = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Euclidean Similarity",
                    "Compares fingertip positions using Euclidean distance."
                ),
                _useEuclideanSimilarity
            );

            _useCurlSimilarity = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Curl Similarity",
                    "Compares how much each finger is curled."
                ),
                _useCurlSimilarity
            );

            _usePalmSimilarity = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Palm Similarity",
                    "Compares palm vectors with cosine similarity."
                ),
                _usePalmSimilarity
            );

            _useWristVerticalSimilarity = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Wrist Vertical Similarity",
                    "Requires wrist to be near a specific vertical angle (0=horizontal)."
                ),
                _useWristVerticalSimilarity
            );

            _wristVerticalTolerance = EditorGUILayout.Slider(
                new GUIContent(
                    "Wrist Vertical Tolerance",
                    "Max deviation from reference wristVerticalDot."
                ),
                _wristVerticalTolerance, 0f, 1f
            );

            _maxFingertipDistance = EditorGUILayout.FloatField(
                new GUIContent(
                    "Max Fingertip Distance",
                    "Maximum distance each fingertip can deviate from its target pose position and still count as a match."
                ),
                _maxFingertipDistance
            );

            _similarityThreshold = Mathf.Clamp01(
                EditorGUILayout.FloatField(
                    new GUIContent(
                        "Similarity Threshold",
                        "Minimum similarity required (0–1) for the pose to be considered a match."
                    ),
                    _similarityThreshold
                )
            );
            
            EditorGUILayout.Space();

            // Show current hand metadata
            var hand = _useLeftHand ? _handLandmarker.LeftFreeHand : _handLandmarker.RightFreeHand;
            if (!hand || !hand.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox($"{(_useLeftHand ? "Left" : "Right")} hand not detected.", MessageType.Info);
            } else {
                // Finger Curls
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Finger Curls", EditorStyles.boldLabel);
                DrawProgressBar("Thumb", hand.Metadata.thumbCurl);
                DrawProgressBar("Index", hand.Metadata.indexCurl);
                DrawProgressBar("Middle", hand.Metadata.middleCurl);
                DrawProgressBar("Ring", hand.Metadata.ringCurl);
                DrawProgressBar("Pinky", hand.Metadata.pinkyCurl);
                DrawProgressBar("Average", hand.Metadata.AverageCurl, Color.yellow);

                EditorGUILayout.Space();

                // Palm Normal
                EditorGUILayout.LabelField("Palm Normal", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("Direction", hand.Metadata.palmNormal);

                EditorGUILayout.Space();

                // Fingertip Positions
                EditorGUILayout.LabelField("Fingertip Positions (relative to wrist)", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("Thumb Tip", hand.Metadata.thumbTip);
                EditorGUILayout.Vector3Field("Index Tip", hand.Metadata.indexTip);
                EditorGUILayout.Vector3Field("Middle Tip", hand.Metadata.middleTip);
                EditorGUILayout.Vector3Field("Ring Tip", hand.Metadata.ringTip);
                EditorGUILayout.Vector3Field("Pinky Tip", hand.Metadata.pinkyTip);

                EditorGUILayout.Space();

                // Wrist Rotation
                EditorGUILayout.LabelField("Wrist Rotation", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("Euler Angles", hand.Metadata.wristRotation.eulerAngles);

                EditorGUILayout.Space();

                // Wrist Vertical
                EditorGUILayout.LabelField("Wrist Vertical", EditorStyles.boldLabel);
                DrawProgressBar("Dot", (hand.Metadata.wristVerticalDot + 1f) / 2f, Color.cyan);
                EditorGUILayout.LabelField($"Raw Value: {hand.Metadata.wristVerticalDot:F2} (1=up, 0=horizontal, -1=down)");

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                // Capture Button
                GUI.enabled = _targetPose;
                if (GUILayout.Button("Capture to Pose Asset", GUILayout.Height(30)))
                {
                    CaptureToAsset(hand.Metadata);
                }
                GUI.enabled = true;

                if (!_targetPose)
                {
                    EditorGUILayout.HelpBox("Assign a HandPose asset to capture to.", MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();

            // Force repaint to update values
            Repaint();
        }

        private void DrawProgressBar(string label, float value, Color? color = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(60));

            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(18));

            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // Fill
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
            EditorGUI.DrawRect(fillRect, color ?? Color.green);

            // Value text
            GUI.Label(rect, $" {value:F2}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void CaptureToAsset(HandMetadata meta)
        {
            Undo.RecordObject(_targetPose, "Capture Hand Pose");
            _targetPose.referenceMetadata = meta;
            _targetPose.isLeftHand = _useLeftHand;
            _targetPose.useEuclideanSimilarity = _useEuclideanSimilarity;
            _targetPose.useCurlSimilarity = _useCurlSimilarity;
            _targetPose.usePalmSimilarity = _usePalmSimilarity;
            _targetPose.useWristVerticalSimilarity = _useWristVerticalSimilarity;
            _targetPose.wristVerticalTolerance = _wristVerticalTolerance;
            _targetPose.similarityThreshold = _similarityThreshold;
            if (string.IsNullOrWhiteSpace(_targetPose.poseName)) _targetPose.poseName = _targetPose.name;
            EditorUtility.SetDirty(_targetPose);
            AssetDatabase.SaveAssets();
            Debug.Log($"Captured pose to {_targetPose.name}");
        }
    }
}
