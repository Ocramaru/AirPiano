using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Hand.Utilities;
using Mediapipe.Tasks.Core;
using UnityEngine;

namespace Piano
{
    public class PianoHandLandmarker : MonoBehaviour
    {
        [SerializeField] private int numHands = 2;
        [SerializeField] private float minHandDetectionConfidence = 0.5f;
        [SerializeField] private float minTrackingConfidence = 0.5f;
        [SerializeField] private bool enableWebcamDisplay = true;
        [SerializeField] private UnityEngine.UI.RawImage webcamDisplay;
        [SerializeField] private WebcamVisualizer webcamVisualizer;

        [Header("Hand Prefabs")]
        [SerializeField] private Hand.Hand leftHandPrefab;
        [SerializeField] private Hand.Hand rightHandPrefab;

        [Header("Hand Positioning")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        private PianoHand _leftPianoHand;
        private PianoHand _rightPianoHand;
        private HandLandmarker _handLandmarker;
        private WebCamTexture _webCamTexture;
        private Texture2D _inputTexture;

        // Track which hands were detected this frame
        private bool _leftHandDetected;
        private bool _rightHandDetected;

        private void Awake()
        {
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
#if UNITY_EDITOR
            AssetLoader.Provide(new LocalResourceManager());
#else
            AssetLoader.Provide(new StreamingAssetsResourceManager());
#endif
            yield return AssetLoader.PrepareAssetAsync("hand_landmarker.bytes");
            Debug.Log("Hand landmarker asset loaded!");

            var options = new HandLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: "hand_landmarker.bytes"),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE,
                numHands: numHands,
                minHandDetectionConfidence: minHandDetectionConfidence,
                minTrackingConfidence: minTrackingConfidence
            );
            _handLandmarker = HandLandmarker.CreateFromOptions(options);
            Debug.Log("HandLandmarker created!");

            // Start webcam
            _webCamTexture = new WebCamTexture();
            _webCamTexture.Play();
            yield return new WaitUntil(() => _webCamTexture.width > 100);
            _inputTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, false);
            Debug.Log($"Webcam started: {_webCamTexture.width}x{_webCamTexture.height}");

            if (enableWebcamDisplay && webcamDisplay) webcamDisplay.texture = _webCamTexture;
            if (webcamVisualizer) webcamVisualizer.Initialize(numHands);

            // Spawn hand models
            SpawnHands();
        }

        private void SpawnHands()
        {
            Vector3 spawnPos = transform.position + spawnOffset;

            if (leftHandPrefab)
            {
                var leftHandObj = Instantiate(leftHandPrefab, spawnPos, Quaternion.identity, transform);
                // Add PianoHand component alongside Hand (PianoHand uses Hand's configuration)
                _leftPianoHand = leftHandObj.gameObject.AddComponent<PianoHand>();
                leftHandObj.gameObject.SetActive(true);
            }

            if (rightHandPrefab)
            {
                var rightHandObj = Instantiate(rightHandPrefab, spawnPos, Quaternion.identity, transform);
                // Add PianoHand component alongside Hand (PianoHand uses Hand's configuration)
                _rightPianoHand = rightHandObj.gameObject.AddComponent<PianoHand>();
                rightHandObj.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_handLandmarker == null || !_inputTexture || !_webCamTexture.isPlaying) return;

            _inputTexture.SetPixels32(_webCamTexture.GetPixels32());
            _inputTexture.Apply();

            using var image = new Mediapipe.Image(_inputTexture);
            var result = _handLandmarker.Detect(image);
            
            // Show on Webcam
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            if (webcamVisualizer) webcamVisualizer.DrawLandmarks(result.handLandmarks, result.handedness);

            UpdateHandModels(result);
        }

        private void UpdateHandModels(HandLandmarkerResult result)
        {
            _leftHandDetected = false; _rightHandDetected = false;

            if (result.handedness != null) UpdateSpecificHandModels(result.handLandmarks, result.handedness);

            // Hide hands that weren't detected this frame
            if (!_leftHandDetected && _leftPianoHand) _leftPianoHand.gameObject.SetActive(false);
            if (!_rightHandDetected && _rightPianoHand) _rightPianoHand.gameObject.SetActive(false);
        }

        private void UpdateSpecificHandModels(List<NormalizedLandmarks> landmarkList, List<Classifications> handednessList)
        {
                for (int i = 0; i < landmarkList.Count; i++)
                {
                    if (i >= handednessList.Count) break;
                    var landmarks = landmarkList[i];
                    var handedness = handednessList[i];

                    if (handedness.categories == null || handedness.categories.Count == 0) continue;

                    bool isMediaPipeLeft = handedness.categories[0].categoryName == "Left";

                    // MediaPipe "Left" = user's right hand (mirrored webcam)
                    switch (isMediaPipeLeft)
                    {
                        case true when _rightPianoHand:
                            _rightPianoHand.UpdateFromLandmarks(landmarks);
                            _rightPianoHand.gameObject.SetActive(true);
                            _rightHandDetected = true;
                            break;
                        case false when _leftPianoHand:
                            _leftPianoHand.UpdateFromLandmarks(landmarks);
                            _leftPianoHand.gameObject.SetActive(true);
                            _leftHandDetected = true;
                            break;
                    }
                }
        }

        private void OnDestroy()
        {
            _webCamTexture?.Stop();
            _handLandmarker?.Close();
        }
    }
}
