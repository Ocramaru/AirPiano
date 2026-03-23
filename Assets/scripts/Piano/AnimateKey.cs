using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;

// Currently not used, Just basic outline I need to fiddle with once I try to implement.
namespace Piano
{
    public class AnimateKey : MonoBehaviour
    {
        public GameObject KeyboardRoot;

        [Header("Spring Settings")]
        public float maxPressDepth = 0.02f;

        private Rigidbody[] keyRigidbodies;
        private Vector3[] restPositions;
    
        private void Start()
        {
            CollectKeys();
            StartCoroutine(Initialize());
        }

        private System.Collections.IEnumerator Initialize()
        {
#if UNITY_EDITOR
            AssetLoader.Provide(new LocalResourceManager());
            yield return AssetLoader.PrepareAssetAsync("hand_landmarker.bytes");
            Debug.Log("Asset loaded!");
#endif
        }

        private void CollectKeys()
        {
            if (!KeyboardRoot) return;

            int childCount = KeyboardRoot.transform.childCount;
            keyRigidbodies = new Rigidbody[childCount];
            restPositions = new Vector3[childCount];

            for (int i = 0; i < childCount; i++)
            {
                Transform keyTransform = KeyboardRoot.transform.GetChild(i);

                Rigidbody rb = keyTransform.GetComponent<Rigidbody>();
                if (!rb)
                {
                    rb = keyTransform.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.constraints = (RigidbodyConstraints)122;  // Freeze Z and X and Rotations
                }
            
                keyRigidbodies[i] = rb;
                restPositions[i] = keyRigidbodies[i].transform.localPosition;
            }

            Debug.Log($"Keyboard Initialized with {childCount} keys");
        }
    
        private void FixedUpdate()
        {
            // Sping Back
            if (keyRigidbodies == null) return;

            for (int i = 0; i < keyRigidbodies.Length; i++)
            {
                if (!keyRigidbodies[i]) continue;

                var distanceFromInitial = keyRigidbodies[i].position.y - restPositions[i].y;
                if (distanceFromInitial > 0)
                {
                    Debug.Log($"{keyRigidbodies[i].name} is different from its initial position");
                }
            }
        }
    }
}
