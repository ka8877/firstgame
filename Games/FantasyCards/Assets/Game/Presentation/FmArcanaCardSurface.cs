using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Presentation
{
    // Read-only presentation identity. The surrounding hit slot may be wide,
    // but this visible surface uses a real fitted 5:7 portrait silhouette.
    public sealed class FmArcanaCardSurface : MonoBehaviour
    {
        public string DefinitionId;
        public CardRarity Grade;
        public float RenderedAspect
        {
            get { var r = ((RectTransform)transform).rect; return r.height > 0 ? r.width / r.height : 0; }
        }
    }
}
