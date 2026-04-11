using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Hand.Utilities;
using Mediapipe.Tasks.Core;
using TMPro;
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

        [Header("Hand Prefabs")] [SerializeField]
        private Hand.Hand leftHandPrefab;

        [SerializeField] private Hand.Hand rightHandPrefab;

        [Header("Free Hand Display (corner overlay)")]
        [Tooltip("Parent transform for free hands in corner overlay area")]
        [SerializeField]
        private Transform freeHandContainer;

        [Tooltip("Separate camera for corner rendering (optional)")] [SerializeField]
        private Camera overlayCamera;

        [Header("Hand Positioning")] [SerializeField]
        private Vector3 pianoSpawnOffset = Vector3.zero;
        
        [Header("Calibration")]
        [SerializeField] private float calibrationCountdown = 3f;
        private float _calibrationCountdown;
        private bool _isCalibrating;
        private bool _isCalibrated;
        [SerializeField] private TMP_Text calibrationCountdownText;

        // Free hands (raw tracking)
        private Hand.Hand _leftFreeHand;
        private Hand.Hand _rightFreeHand;

        // Piano hands (driven by free hands)
        private PianoHand _leftPianoHand;
        private PianoHand _rightPianoHand;

        private HandLandmarker _handLandmarker;
        private WebCamTexture _webCamTexture;
        private Texture2D _inputTexture;

        private bool _leftHandDetected;
        private bool _rightHandDetected;

        private const float FreeHandSeparation = 0.25f;

        // Public accessors
        public Hand.Hand LeftFreeHand => _leftFreeHand;
        public Hand.Hand RightFreeHand => _rightFreeHand;
        public PianoHand LeftPianoHand => _leftPianoHand;
        public PianoHand RightPianoHand => _rightPianoHand;

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

            _webCamTexture = new WebCamTexture();
            _webCamTexture.Play();
            yield return new WaitUntil(() => _webCamTexture.width > 100);
            _inputTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, false);
            Debug.Log($"Webcam started: {_webCamTexture.width}x{_webCamTexture.height}");

            if (enableWebcamDisplay && webcamDisplay) webcamDisplay.texture = _webCamTexture;
            if (webcamVisualizer) webcamVisualizer.Initialize(numHands);

            // Spawn free hands (raw tracking, in overlay container if specified)
            Transform freeParent = freeHandContainer ? freeHandContainer : transform;
            Vector3 freeBasePos = freeParent.position;

            if (leftHandPrefab)
            {
                Vector3 leftPos = freeBasePos + new Vector3(-FreeHandSeparation / 2f, 0f, 0f);
                _leftFreeHand = Instantiate(leftHandPrefab, leftPos, Quaternion.identity, freeParent);
                _leftFreeHand.gameObject.name = "LeftFreeHand";
                _leftFreeHand.gameObject.SetActive(true);
            }

            if (rightHandPrefab)
            {
                Vector3 rightPos = freeBasePos + new Vector3(FreeHandSeparation / 2f, 0f, 0f);
                _rightFreeHand = Instantiate(rightHandPrefab, rightPos, Quaternion.identity, freeParent);
                _rightFreeHand.gameObject.name = "RightFreeHand";
                _rightFreeHand.gameObject.SetActive(false);
            }

            // Spawn piano hands (same prefab + PianoHand component, driven by free hands)
            Vector3 pianoSpawnPos = transform.position + pianoSpawnOffset;

            if (leftHandPrefab)
            {
                var leftPianoObj = Instantiate(leftHandPrefab, pianoSpawnPos, Quaternion.identity, transform);
                leftPianoObj.gameObject.name = "LeftPianoHand";
                _leftPianoHand = leftPianoObj.gameObject.AddComponent<PianoHand>();
                _leftPianoHand.freeHand = _leftFreeHand;
                leftPianoObj.gameObject.SetActive(true);
            }

            if (rightHandPrefab)
            {
                var rightPianoObj = Instantiate(rightHandPrefab, pianoSpawnPos, Quaternion.identity, transform);
                rightPianoObj.gameObject.name = "RightPianoHand";
                _rightPianoHand = rightPianoObj.gameObject.AddComponent<PianoHand>();
                _rightPianoHand.freeHand = _rightFreeHand;
                rightPianoObj.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_handLandmarker == null || !_inputTexture || !_webCamTexture.isPlaying) return;

            _inputTexture.SetPixels32(_webCamTexture.GetPixels32());
            _inputTexture.Apply();

            using var image = new Mediapipe.Image(_inputTexture);
            var result = _handLandmarker.Detect(image);

            if (webcamVisualizer) webcamVisualizer.DrawLandmarks(result.handLandmarks, result.handedness);
            if (_isCalibrating) UpdateCalibrationCountdown();
            
            UpdateHandModels(result);
        }

        private void UpdateHandModels(HandLandmarkerResult result)
        {
            _leftHandDetected = false;
            _rightHandDetected = false;

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
                    if (isMediaPipeLeft)
                    {
                        // Update right free hand from landmarks
                        if (_rightFreeHand)
                        {
                            _rightFreeHand.UpdateFromLandmarks(landmarks);
                            _rightFreeHand.gameObject.SetActive(true);
                        }

                        // Piano hand updates itself in LateUpdate from free hand
                        if (_rightPianoHand)
                            _rightPianoHand.gameObject.SetActive(true);
                        _rightHandDetected = true;
                    }
                    else
                    {
                        // Update left free hand from landmarks
                        if (_leftFreeHand)
                        {
                            _leftFreeHand.UpdateFromLandmarks(landmarks);
                            _leftFreeHand.gameObject.SetActive(true);
                        }

                        // Piano hand updates itself in LateUpdate from free hand
                        if (_leftPianoHand)
                            _leftPianoHand.gameObject.SetActive(true);
                        _leftHandDetected = true;
                    }
                }
            }

            // Handle hands that weren't detected this frame
            if (!_leftHandDetected && _leftFreeHand)
            {
                _leftFreeHand.UpdateFromLandmarks(default);
                if (!_leftFreeHand.IsHolding)
                {
                    _leftFreeHand.gameObject.SetActive(false);
                    if (_leftPianoHand) _leftPianoHand.gameObject.SetActive(false);
                }
            }

            if (!_rightHandDetected && _rightFreeHand)
            {
                _rightFreeHand.UpdateFromLandmarks(default);
                if (!_rightFreeHand.IsHolding)
                {
                    _rightFreeHand.gameObject.SetActive(false);
                    if (_rightPianoHand) _rightPianoHand.gameObject.SetActive(false);
                }
            }
        }
        
        public void CalibratePianoHands()
        {
            _isCalibrating = true;
            _calibrationCountdown = calibrationCountdown;
            calibrationCountdownText.transform.parent.gameObject.SetActive(true);
        }

        private void UpdateCalibrationCountdown()
        {
            _calibrationCountdown -= Time.deltaTime;
            calibrationCountdownText.text = $"{Mathf.CeilToInt(_calibrationCountdown)}s";
            if (!(_calibrationCountdown < 0)) return;
            
            // Now Calibrate!
            _calibrationCountdown = 0;
            _leftPianoHand.Calibrate();
            _rightPianoHand.Calibrate();
            _isCalibrating = false;
            Debug.Log($"Calibration status: Left ({_leftPianoHand.IsCalibrated}) | Right ({_rightPianoHand.IsCalibrated})");
            _isCalibrated = _leftPianoHand.IsCalibrated && _rightPianoHand.IsCalibrated;
            calibrationCountdownText.transform.parent.gameObject.SetActive(false);
        }

        public void DebugCall()
        {
            Debug.Log("Called Debug Call");

        }

    private void OnDestroy()
        {
            _webCamTexture?.Stop();
            _handLandmarker?.Close();
        }
    }
}
