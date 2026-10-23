using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Cover the allotted frame without stretching the illustration or crossing atlas cells.
    [RequireComponent(typeof(RawImage))]
    public sealed class IllustrationCrop : MonoBehaviour
    {
        public Rect SourceUv = new Rect(0, 0, 1, 1);
        public Vector2 Focus = new Vector2(.5f, .62f);
        RawImage image;

        void OnEnable() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();
        public void Refresh()
        {
            if (image == null) image = GetComponent<RawImage>();
            if (image == null || image.texture == null) return;
            var frame = ((RectTransform)transform).rect;
            if (frame.width <= 0 || frame.height <= 0) return;
            float sourceAspect = image.texture.width * SourceUv.width / (image.texture.height * SourceUv.height);
            float targetAspect = frame.width / frame.height;
            Rect uv = SourceUv;
            if (targetAspect < sourceAspect)
            {
                uv.width *= targetAspect / sourceAspect;
                uv.x += (SourceUv.width - uv.width) * Focus.x;
            }
            else
            {
                uv.height *= sourceAspect / targetAspect;
                uv.y += (SourceUv.height - uv.height) * Focus.y;
            }
            image.uvRect = uv;
        }
    }
}
