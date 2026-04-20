using System.Collections.Generic;
using UnityEngine;

namespace Piano
{
    public class AnimatePiano : MonoBehaviour
    {
        public GameObject KeyboardRoot;

        [Header("Key Detection")]
        [Tooltip("How far a key must be pressed (negative Y) to trigger a note")]
        public float pressThreshold = 0.005f;

        [Tooltip("Hysteresis: key must return this close to rest to release")]
        public float releaseThreshold = 0.002f;

        [Header("Piano Settings")]
        [Tooltip("Starting MIDI note number for the first key (21 = A0 for 87-key piano)")]
        public int startingMidiNote = 21;

        [Tooltip("Volume of piano notes")]
        [Range(0f, 1f)]
        public float volume = 0.8f;

        [Header("Spring Settings")]
        public float maxPressDepth = 0.02f;

        [Header("Playback")]
        [Tooltip("Legato = notes sustain until key released. Staccato = notes play full sample")]
        public bool legato = true;

        [Tooltip("How quickly notes fade out when key is released (seconds)")]
        public float releaseTime = 0.15f;

        [Header("Debug")]
        [Tooltip("Test note to play with the Test button")]
        public int testNote = 60;

        private Transform[] _keyTransforms;
        private Rigidbody[] _keyRigidbodies;
        private Vector3[] _restLocalPositions;
        private bool[] _keyPressed;

        // Audio
        private Dictionary<int, AudioClip> _sampleClips;
        private int[] _sampledNotes;
        private AudioSource[] _audioSources;
        private int[] _keyToVoice;  // Maps key index to voice index (-1 if not playing)
        private const int PolyphonyLimit = 32;

        private void Start()
        {
            LoadSamples();
            CreateAudioSources();
            CollectKeys();
        }

        private void LoadSamples()
        {
            // Sampled notes from Salamander Grand Piano (every 3 semitones)
            _sampledNotes = new int[]
            {
                21, 24, 27, 30, 33, 36, 39, 42, 45, 48,
                51, 54, 57, 60, 63, 66, 69, 72, 75, 78,
                81, 84, 87, 90, 93, 96, 99, 102, 105, 108
            };

            _sampleClips = new Dictionary<int, AudioClip>();

            // Load all audio clips from the PianoSamples folder
            AudioClip[] allClips = Resources.LoadAll<AudioClip>("PianoSamples");
            Debug.Log($"Found {allClips.Length} audio clips in PianoSamples folder");

            foreach (AudioClip clip in allClips)
            {
                // Parse MIDI note from filename (e.g., "021_A0v08" -> 21)
                string name = clip.name;
                if (name.Length >= 3 && int.TryParse(name.Substring(0, 3), out int midiNote))
                {
                    _sampleClips[midiNote] = clip;
                    Debug.Log($"Loaded sample for MIDI {midiNote}: {name}");
                }
            }

            Debug.Log($"Mapped {_sampleClips.Count} piano samples");
        }

        private string GetNoteName(int midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        private void CreateAudioSources()
        {
            _audioSources = new AudioSource[PolyphonyLimit];

            // Create a hidden container for audio voices
            GameObject voiceContainer = new GameObject("PianoVoices");
            voiceContainer.transform.SetParent(transform);

            for (int i = 0; i < PolyphonyLimit; i++)
            {
                GameObject audioObj = new GameObject($"Voice_{i}");
                audioObj.transform.SetParent(voiceContainer.transform);
                audioObj.hideFlags = HideFlags.HideInHierarchy;
                AudioSource source = audioObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D sound
                _audioSources[i] = source;
            }
        }

        private void CollectKeys()
        {
            if (!KeyboardRoot) return;

            int childCount = KeyboardRoot.transform.childCount;
            _keyTransforms = new Transform[childCount];
            _keyRigidbodies = new Rigidbody[childCount];
            _restLocalPositions = new Vector3[childCount];
            _keyPressed = new bool[childCount];
            _keyToVoice = new int[childCount];

            for (int i = 0; i < childCount; i++)
            {
                Transform keyTransform = KeyboardRoot.transform.GetChild(i);
                _keyTransforms[i] = keyTransform;
                _keyRigidbodies[i] = keyTransform.GetComponent<Rigidbody>();
                _restLocalPositions[i] = keyTransform.localPosition;
                _keyPressed[i] = false;
                _keyToVoice[i] = -1;  // No voice assigned

                if (_keyRigidbodies[i] == null)
                {
                    Debug.LogWarning($"Key {i} ({keyTransform.name}) is missing a Rigidbody!");
                }
            }

            Debug.Log($"Keyboard initialized with {childCount} keys (MIDI notes {startingMidiNote} - {startingMidiNote + childCount - 1})");
        }

        private void FixedUpdate()
        {
            if (_keyTransforms == null) return;

            for (int i = 0; i < _keyTransforms.Length; i++)
            {
                if (!_keyTransforms[i]) continue;

                Vector3 restPos = _restLocalPositions[i];
                Vector3 currentPos = _keyTransforms[i].localPosition;

                // Calculate displacement on all axes
                Vector3 delta = restPos - currentPos;

                // Keys move in Z axis (positive delta.z = pressed)
                float displacement = delta.z;
                
                // Clamp to maxPressDepth
                if (displacement > maxPressDepth)
                {
                    Vector3 clampedPos = currentPos;
                    clampedPos.z = restPos.z - maxPressDepth;
                    _keyRigidbodies[i].MovePosition(_keyTransforms[i].parent.TransformPoint(clampedPos));
                    displacement = maxPressDepth;
                } else if (displacement < 0) // Prevent key from going above rest position
                {
                    _keyRigidbodies[i].MovePosition(_keyTransforms[i].parent.TransformPoint(restPos));
                    displacement = 0;
                }

                if (!_keyPressed[i] && displacement >= pressThreshold) {
                    _keyPressed[i] = true;
                    PlayNote(i, startingMidiNote + i);
                } else if (_keyPressed[i] && displacement <= releaseThreshold) {
                    _keyPressed[i] = false;
                    ReleaseNote(i);
                }
            }
        }

        private void PlayNote(int keyIndex, int midiNote)
        {
            // Find closest sampled note
            int closestSample = FindClosestSample(midiNote);
            if (!_sampleClips.TryGetValue(closestSample, out AudioClip clip))
            {
                Debug.LogWarning($"No sample found for MIDI note {midiNote}");
                return;
            }

            // Calculate pitch shift
            int semitoneOffset = midiNote - closestSample;
            float pitch = Mathf.Pow(2f, semitoneOffset / 12f);

            // Find available audio source
            int voiceIndex = GetAvailableVoiceIndex();
            AudioSource source = _audioSources[voiceIndex];

            // Stop any existing coroutine fading this voice
            source.volume = volume;
            source.clip = clip;
            source.pitch = pitch;
            source.Play();

            // Track which voice this key is using
            _keyToVoice[keyIndex] = voiceIndex;
        }

        private void ReleaseNote(int keyIndex)
        {
            if (!legato) return;  // Staccato mode: let notes play out naturally

            int voiceIndex = _keyToVoice[keyIndex];
            if (voiceIndex < 0 || voiceIndex >= _audioSources.Length) return;

            AudioSource source = _audioSources[voiceIndex];
            if (source.isPlaying)
            {
                StartCoroutine(FadeOutVoice(source, releaseTime));
            }

            _keyToVoice[keyIndex] = -1;
        }

        private System.Collections.IEnumerator FadeOutVoice(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration && source.isPlaying)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            source.Stop();
            source.volume = volume;  // Reset for next use
        }

        private int FindClosestSample(int midiNote)
        {
            int closest = _sampledNotes[0];
            int minDistance = Mathf.Abs(midiNote - closest);

            foreach (int sample in _sampledNotes)
            {
                int distance = Mathf.Abs(midiNote - sample);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = sample;
                }
            }

            return closest;
        }

        private int GetAvailableVoiceIndex()
        {
            // First, find a voice that's not playing
            for (int i = 0; i < _audioSources.Length; i++)
            {
                if (!_audioSources[i].isPlaying)
                    return i;
            }

            // All voices busy - find the quietest one to steal
            int quietest = 0;
            float lowestVolume = _audioSources[0].volume;
            for (int i = 1; i < _audioSources.Length; i++)
            {
                if (_audioSources[i].volume < lowestVolume)
                {
                    lowestVolume = _audioSources[i].volume;
                    quietest = i;
                }
            }
            return quietest;
        }

        // Right-click on component -> Test Play Note
        [ContextMenu("Test Play Note")]
        public void TestPlayNote()
        {
            if (_sampleClips == null || _sampleClips.Count == 0)
            {
                LoadSamples();
                CreateAudioSources();
            }
            if (_keyToVoice == null)
            {
                _keyToVoice = new int[1];
                _keyToVoice[0] = -1;
            }
            PlayNote(0, testNote);
        }
    }
}
