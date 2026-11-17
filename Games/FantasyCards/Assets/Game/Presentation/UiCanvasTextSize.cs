using TMPro;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

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
        bool responsiveCaption;
        int captionLines;
        float captionWidthAdjustment, lastCssRatio = -1;
        Vector2 lastCanvas = new Vector2(-1, -1), lastBox = new Vector2(-1, -1);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern float FmClientCssPixelRatio();
        static int cssRatioFrame = -1;
        static float cachedCssRatio = 1;
#endif

        public static float ClientPixelScale(Canvas value) => Mathf.Min(
            Mathf.Max(1, value.pixelRect.width) / 1440f,
            Mathf.Max(1, value.pixelRect.height) / 1080f);

        public static Vector2 ClientCssSize(Canvas value) => value.pixelRect.size / CssPixelRatio();
        public static float ClientCssPixelScale(Canvas value) => ClientPixelScale(value) / CssPixelRatio();

        public void Configure(float size, bool keepPhysicalFloor = false, bool oneLine = false)
        {
            responsiveCaption = false;
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

        // Opt-in for narrow status/notice boxes. Other labels retain their
        // existing sizing contract. CSS pixels stay stable at WebGL DPR 1/2.
        public void ConfigureResponsiveCaption(float size, int fittingLines = 1, float widthAdjustment = 0)
        {
            authoredSize = size; responsiveCaption = true;
            captionLines = Mathf.Max(1, fittingLines);
            captionWidthAdjustment = Mathf.Clamp(widthAdjustment, 0, 20);
            label = GetComponent<TMP_Text>(); canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            lastCanvas = new Vector2(-1, -1); lastCssRatio = -1; Refresh();
        }

        static float CssPixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // One DOM measurement per rendered frame, shared by all labels.
            if (cssRatioFrame == Time.frameCount) return cachedCssRatio;
            cssRatioFrame = Time.frameCount;
            float ratio = FmClientCssPixelRatio();
            cachedCssRatio = ratio > 0 && !float.IsNaN(ratio) && !float.IsInfinity(ratio) ? ratio : 1;
            return cachedCssRatio;
#else
            return 1;
#endif
        }

        void LateUpdate() { Refresh(); }
        void OnRectTransformDimensionsChange() { Refresh(); }

        void Refresh()
        {
            if (label == null || canvas == null || authoredSize <= 0) return;
            var pixels = canvas.pixelRect.size; var box = label.rectTransform.rect.size;
            float cssRatio = CssPixelRatio();
            if (pixels == lastCanvas && box == lastBox && cssRatio == lastCssRatio) return;
            lastCanvas = pixels; lastBox = box; lastCssRatio = cssRatio;
            // Text scales with the contained 4:3 client, including pillarbox
            // layouts, rather than with the outer browser canvas.
            float scale = ClientPixelScale(canvas);
            var cssPixels = pixels / cssRatio;
            float cssScale = scale / cssRatio;
            if (responsiveCaption)
            {
                bool smallCaption = cssPixels.y < 600 && (cssPixels.x < 1400 || cssPixels.x / Mathf.Max(1, cssPixels.y) > 1.95f);
                if (smallCaption)
                {
                    float minimum = 8 / cssScale, maximumCaption = 10 / cssScale;
                    if (captionLines > 1 && box.y > 0 && label.font != null)
                    {
                        var metrics = label.font.faceInfo;
                        float lineRatio = Mathf.Max(1, (metrics.ascentLine - metrics.descentLine) / Mathf.Max(1, metrics.pointSize));
                        maximumCaption = Mathf.Min(maximumCaption, box.y / (captionLines * lineRatio) * .99f);
                    }
                    label.enableAutoSizing = true;
                    label.fontSizeMin = minimum;
                    label.fontSizeMax = label.fontSize = Mathf.Max(minimum, maximumCaption);
                    label.characterWidthAdjustment = captionWidthAdjustment;
                    return;
                }
                label.characterWidthAdjustment = 0;
            }
            bool small = cssPixels.y < 600 && (cssPixels.x < 1400 || cssPixels.x / Mathf.Max(1, cssPixels.y) > 1.95f);
            float floor = physicalFloor || small ? 12 / cssScale : 0;
            float maximum = small ? Mathf.Max(authoredSize, 13 / cssScale) : authoredSize;
            if (singleLine && box.y > 0 && label.font != null)
            {
                var face = label.font.faceInfo;
                float lineRatio = Mathf.Max(1, (face.ascentLine - face.descentLine) / Mathf.Max(1, face.pointSize));
                float available = box.y / lineRatio * (boundedCardText ? .92f : .98f);
                maximum = Mathf.Min(maximum, available);
                if (boundedCardText) floor = Mathf.Min(8f / cssScale, available);
            }
            maximum = Mathf.Max(maximum, floor);
            label.fontSizeMin = Mathf.Min(maximum, Mathf.Max(maximum * .8f, floor));
            label.fontSizeMax = maximum; label.fontSize = maximum;
        }
    }
}
