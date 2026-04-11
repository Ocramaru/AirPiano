using UnityEngine;

public class Settings : MonoBehaviour
{
    private static Settings _instance;
    public static Settings Instance => _instance;

    [Header("EMA Smoothing")]
    [Tooltip("Aka this uses an EMA and the hold duration.")]
    public bool usePreviousHandPositions = true;

    [Header("Hand Tracking")]
    [Tooltip("Hand must be detected for this long before hold will activate (seconds)")]
    public float minPresenceBeforeHold = .5f;

    [Tooltip("How long to hold the last hand position when tracking is lost (seconds)")]
    public float handHoldDuration = 1.5f;
    
    [Range(0.05f, 1f)]
    [Tooltip("Lower = smoother but more lag. Higher = responsive but jittery.")]
    public float landmarkEMAAlpha = 0.3f;

    [Range(0.05f, 1f)]
    [Tooltip("Lower = smoother but more lag. Higher = responsive but jittery.")]
    public float wristRotationEMAAlpha = 0.25f;

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
}
