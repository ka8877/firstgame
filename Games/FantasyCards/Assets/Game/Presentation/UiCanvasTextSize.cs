using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    // CanvasScaler can update after a dynamically-created label. Reapply the
    // authored size when the actual canvas/line box changes, rather than
    // retaining a mobile pixel floor as an oversized desktop point size.
    public sealed class UiCanvasTextSize : MonoBehaviour
    {
        TMP_Text label;
        Canvas canvas;
        float authoredSize;
        bool physicalFloor, singleLine, boundedCardText;
        Vector2 lastCanvas = new Vector2(-1, -1), lastBox = new Vector2(-1, -1);

        public void Configure(float size, bool keepPhysicalFloor = false, bool oneLine = false)
        {
            authoredSize = size; physicalFloor = keepPhysicalFloor; singleLine = oneLine;
            label = GetComponent<TMP_Text>(); canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            lastCanvas = new Vector2(-1, -1); Refresh();
        }

        // A miniature card's line box may be shorter than the general UI's
        // 12px floor. Fit within that box instead of making TMP Ellipsis drop
        // the entire line. Other labels keep their existing sizing contract.
        public void ConfigureCard(float size)
        {
            boundedCardText = true; Configure(size, false, true);
        }

        void LateUpdate() { Refresh(); }
        void OnRectTransformDimensionsChange() { Refresh(); }

        void Refresh()
        {
            if (label == null || canvas == null || authoredSize <= 0) return;
            var pixels = canvas.pixelRect.size; var box = label.rectTransform.rect.size;
            if (pixels == lastCanvas && box == lastBox) return;
            lastCanvas = pixels; lastBox = box;
            // This is the same geometric-average scale as the app's .5
            // CanvasScaler. It is valid before CanvasScaler's next update.
            float scale = Mathf.Sqrt(Mathf.Max(1, pixels.x) / 1920f * Mathf.Max(1, pixels.y) / 1080f);
            bool small = pixels.y < 600 && (pixels.x < 1400 || pixels.x / Mathf.Max(1, pixels.y) > 1.95f);
            float floor = physicalFloor || small ? 12 / scale : 0;
            float maximum = small ? Mathf.Max(authoredSize, 13 / scale) : authoredSize;
            if (singleLine && box.y > 0 && label.font != null)
            {
                var face = label.font.faceInfo;
                float lineRatio = Mathf.Max(1, (face.ascentLine - face.descentLine) / Mathf.Max(1, face.pointSize));
                float available = box.y / lineRatio * (boundedCardText ? .92f : .98f);
                maximum = Mathf.Min(maximum, available);
                if (boundedCardText) floor = Mathf.Min(8f / scale, available);
            }
            maximum = Mathf.Max(maximum, floor);
            label.fontSizeMin = Mathf.Min(maximum, Mathf.Max(maximum * .8f, floor));
            label.fontSizeMax = maximum; label.fontSize = maximum;
        }
    }
}
