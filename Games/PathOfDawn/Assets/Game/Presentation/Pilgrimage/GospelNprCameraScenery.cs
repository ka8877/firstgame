using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>A trigger used only by the exploration camera. Character and
    /// people navigation queries continue to ignore triggers.</summary>
    [DisallowMultipleComponent]
    public sealed class GospelNprCameraScenery : MonoBehaviour
    {
        public static BoxCollider AddBox(Transform parent, Vector3 center, Vector3 size)
        {
            var node = new GameObject("Camera-only scenery clearance");
            node.transform.SetParent(parent, false);
            node.transform.localPosition = center;
            node.AddComponent<GospelNprCameraScenery>();
            var collider = node.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            return collider;
        }
    }
}
