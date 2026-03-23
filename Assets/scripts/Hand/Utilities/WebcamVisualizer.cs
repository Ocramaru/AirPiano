using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;
using UnityEngine.UI;

namespace Hand.Utilities
{
    public class WebcamVisualizer : MonoBehaviour
    {
        [SerializeField] private int dotRadius = 3;
        [SerializeField] private Color32 leftHandColor = Color.green;
        [SerializeField] private Color32 rightHandColor = Color.cyan;
        [SerializeField] private Color32 clearColor = new(0, 0, 0, 0);

        private GameObject[] _layers;
        private Texture2D[] _textures;
        private Color32[][] _pixels;
        private Color32[] _clearPixels;
        private int _width;
        private int _height;
        private int _handCount;
        private int _r2;

        public void Initialize(int handCount)
        {
            _handCount = handCount;

            var rect = GetComponent<RectTransform>().rect;
            _width = (int)rect.width;
            _height = (int)rect.height;

            int pixelCount = _width * _height;
            _clearPixels = new Color32[pixelCount];
            _r2 = dotRadius * dotRadius;

            for (int i = 0; i < pixelCount; i++)
                _clearPixels[i] = clearColor;

            _layers = new GameObject[handCount];
            _textures = new Texture2D[handCount];
            _pixels = new Color32[handCount][];

            for (int h = 0; h < handCount; h++)
            {
                var go = new GameObject($"HandLayer_{h}", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };

                go.GetComponent<RawImage>().texture = texture;
                _layers[h] = go;
                _textures[h] = texture;
                _pixels[h] = new Color32[pixelCount];
            }
        }

        public void DrawLandmarks(IReadOnlyList<NormalizedLandmarks> hands, IReadOnlyList<Classifications> handedness)
        {
            if (hands == null || hands.Count == 0)
            {
                for (int h = 0; h < _handCount; h++)
                    _layers[h].SetActive(false);
                return;
            }

            int handsDrawn = hands.Count < _handCount ? hands.Count : _handCount;

            // Draw active hands
            for (int h = 0; h < handsDrawn; h++)
            {
                bool isLeft = handedness != null && h < handedness.Count &&
                              handedness[h].categories.Count > 0 &&
                              handedness[h].categories[0].categoryName == "Left";
                var color = isLeft ? leftHandColor : rightHandColor;
                var landmarks = hands[h].landmarks;
                var pixels = _pixels[h];

                System.Array.Copy(_clearPixels, pixels, _clearPixels.Length);

                for (int i = 0; i < landmarks.Count; i++)
                {
                    int x = (int)(landmarks[i].x * _width);
                    int y = (int)(landmarks[i].y * _height);
                    DrawDot(pixels, x, y, color);
                }

                _textures[h].SetPixelData(pixels, 0);
                _textures[h].Apply(false);
                _layers[h].SetActive(true);
            }

            // Hide unused layers
            for (int h = handsDrawn; h < _handCount; h++)
                _layers[h].SetActive(false);
        }

        private void DrawDot(Color32[] pixels, int cx, int cy, Color32 color)
        {
            int yMin = cy - dotRadius < 0 ? -cy : -dotRadius;
            int yMax = cy + dotRadius >= _height ? _height - 1 - cy : dotRadius;
            int xMin = cx - dotRadius < 0 ? -cx : -dotRadius;
            int xMax = cx + dotRadius >= _width ? _width - 1 - cx : dotRadius;

            for (int dy = yMin; dy <= yMax; dy++)
            {
                int rowOffset = (cy + dy) * _width;
                for (int dx = xMin; dx <= xMax; dx++)
                {
                    if (dx * dx + dy * dy > _r2) continue;
                    pixels[rowOffset + cx + dx] = color;
                }
            }
        }

        private void OnDestroy()
        {
            if (_textures == null) return;
            foreach (var texture in _textures)
                if (texture) Destroy(texture);
        }
    }
}