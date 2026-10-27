namespace FantasyCards.Core
{
    // Whitelist of original IDs whose entire combat technique is implemented.
    // Source skill codes and full source records are preserved in
    // Tools/Data/Restoration/selected-records.json. Authoring cannot accidentally
    // turn every copy of a similarly named later card into this variant.
    public static class RestorationCardSkills
    {
        public const string StrongPoison = "fm_3061320559";
        public const string BeastEvade = "fm_3061320560";
        public const string LandRuin = "fm_3022820395";
        public const string DimensionShockwave = "fm_15052733622";
        public static bool EvadesBeast(UnitState defender, UnitState attacker)
        { return defender.definitionId == BeastEvade && (CardCatalog.Get(attacker.definitionId).capabilityMask & 1) != 0 && CardCatalog.Get(attacker.definitionId).level >= CardCatalog.Get(defender.definitionId).level; }
    }
}
