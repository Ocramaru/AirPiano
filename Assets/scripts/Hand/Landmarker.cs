using System.Collections;
using Hand.Utilities;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;

namespace Hand
{
    public class Landmarker : MonoBehaviour
    {
        [SerializeField] private int numHands = 2;
        [SerializeField] private float minHandDetectionConfidence = 0.5f;
        [SerializeField] private float minTrackingConfidence = 0.5f;
        [SerializeField] private bool enableWebcamDisplay = true;
        [SerializeField] private UnityEngine.UI.RawImage webcamDisplay;
        [SerializeField] private WebcamVisualizer webcamVisualizer;
        private WebCamTexture _webCamTexture;
        private Texture2D _inputTexture;
        
        [Header("Hand Prefabs")]
        [SerializeField] private Hand leftHandPrefab;
        [SerializeField] private Hand rightHandPrefab;
        
        [Header("Hand Positioning")]
        [SerializeField] private Vector3 leftOffset = Vector3.zero;
        [SerializeField] private Vector3 rightOffset = Vector3.zero;
        
        private Hand _leftHandModel;
        private Hand _rightHandModel;
        private HandLandmarker _handLandmarker;

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
            if (leftHandPrefab)
            {
                _leftHandModel = Instantiate(leftHandPrefab, transform.position + leftOffset, Quaternion.identity, transform);
                // _leftHandModel.SetScreenScale(screenScale);
                _leftHandModel.gameObject.SetActive(true);
            }

            if (rightHandPrefab)
            {
                _rightHandModel = Instantiate(rightHandPrefab, transform.position + rightOffset, Quaternion.identity, transform);
                // _rightHandModel.SetScreenScale(screenScale);
                _rightHandModel.gameObject.SetActive(false);
            }
        }

        // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
        private void Update()
        {
            if (_handLandmarker == null || !_inputTexture || !_webCamTexture.isPlaying) return;

            _inputTexture.SetPixels32(_webCamTexture.GetPixels32());
            _inputTexture.Apply();

            using var image = new Mediapipe.Image(_inputTexture);
            var result = _handLandmarker.Detect(image);
            
            // Show on Webcam
            if (webcamVisualizer) webcamVisualizer.DrawLandmarks(result.handLandmarks, result.handedness);

            // Update Hand Models
            _leftHandDetected = false; _rightHandDetected = false;
            if (result.handedness != null)
            {
                for (int i = 0; i < result.handLandmarks.Count; i++)
                {
                    if (i >= result.handedness.Count) break;
                    var landmarks = result.handLandmarks[i];
                    var handedness = result.handedness[i];

                    if (handedness.categories == null || handedness.categories.Count == 0) continue;

                    bool isMediaPipeLeft = handedness.categories[0].categoryName == "Left";

                    // MediaPipe "Left" = user's right hand (mirrored webcam)
                    switch (isMediaPipeLeft)
                    {
                        case true when _rightHandModel:
                            _rightHandModel.UpdateFromLandmarks(landmarks);
                            _rightHandModel.gameObject.SetActive(true);
                            _rightHandDetected = true;
                            break;
                        case false when _leftHandModel:
                            _leftHandModel.UpdateFromLandmarks(landmarks);
                            _leftHandModel.gameObject.SetActive(true);
                            _leftHandDetected = true;
                            break;
                    }
                }
            }
            
            // Handle hands that weren't detected this frame
            if (!_leftHandDetected && _leftHandModel)
            {
                _leftHandModel.UpdateFromLandmarks(default);
                if (!_leftHandModel.IsHolding) _leftHandModel.gameObject.SetActive(false);
            }
            if (!_rightHandDetected && _rightHandModel)
            {
                _rightHandModel.UpdateFromLandmarks(default);
                if (!_rightHandModel.IsHolding) _rightHandModel.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _webCamTexture?.Stop();
            _handLandmarker?.Close();
        }
    }
}