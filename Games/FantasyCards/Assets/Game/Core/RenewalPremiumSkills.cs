using System.Linq;
namespace FantasyCards.Core
{
    public static class RenewalPremiumSkills
    {
        public static int OverlordAttackCoinBonus(UnitState unit) => unit.definitionId == "fm_5112322065" ? unit.equipment.Count(c => CardCatalog.Get(c.definitionId).name == "플레임스트링") : 0;
        public static int OverlordChanceBonus(UnitState unit) => 10 * OverlordAttackCoinBonus(unit);
    }
}
