using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Presentation
{
    // Read-only presentation identity. The surrounding hit slot may be wide,
    // while the visible surface preserves the complete original printed source.
    public sealed class FmArcanaCardSurface : MonoBehaviour
    {
        public string DefinitionId;
        public CardRarity Grade;
        public float SourceAspect;
        public bool UsesFullPrint;
        public float RenderedAspect
        {
            get { var r = ((RectTransform)transform).rect; return r.height > 0 ? r.width / r.height : 0; }
        }
    }
}
