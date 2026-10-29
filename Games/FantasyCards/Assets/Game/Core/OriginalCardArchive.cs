using System;

namespace FantasyCards.Core
{
    [Serializable] public sealed class OriginalArchiveCard
    {
        public string id, name, artResource, sourceUrl, sourceRules, unsupportedReason;
        public CardRarity rarity;
        public Element element;
        public int level;
        public int categoryCode, sourceRarityCode, rankCode, releaseDate, attack, defense, health;
        public bool playable;
        public OriginalArchiveCard(string id, string name, string artResource, string sourceUrl,
            string sourceRules, string unsupportedReason, CardRarity rarity, Element element, int level)
        {
            this.id = id; this.name = name; this.artResource = artResource; this.sourceUrl = sourceUrl;
            this.sourceRules = sourceRules; this.unsupportedReason = unsupportedReason;
            this.rarity = rarity; this.element = element; this.level = level;
        }
    }

    // Complete source catalog. A positive playable flag is backed by CardCatalog;
    // unsupported originals retain their rules and never enter decks silently.
    public static class OriginalCardArchive
    {
        public static readonly OriginalArchiveCard[] All = OriginalArchiveData.Create();
        public static OriginalArchiveCard Get(string id)
        { return Array.Find(All, card => card.id == id) ?? throw new ArgumentException("알 수 없는 원본 도감 카드: " + id); }
    }
}
