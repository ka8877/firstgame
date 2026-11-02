using UnityEngine;
namespace FantasyCards.Presentation
{
    // Read-only identity of each actual sealed draw position, not an award source.
    public sealed class FmRestageAcquiredCard : MonoBehaviour
    {
        public string CardId;
        public int SlotIndex, OwnedCount;
    }
}
