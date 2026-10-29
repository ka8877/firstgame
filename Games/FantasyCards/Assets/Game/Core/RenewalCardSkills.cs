using System;
using System.Linq;
namespace FantasyCards.Core
{
    // Rules verified against the late official manual linked from the 2017 FAQ,
    // plus complete original card records. The generated code table is admission,
    // not a fallback that erases unknown techniques.
    public static partial class RenewalCardSkills
    {
        public static bool DoubleChain(string id) => long.TryParse(id.StartsWith("fm_") ? id.Substring(3) : "", out long n) && n >= 15100741649L && n <= 15100741655L;
        public static bool IdCopy(string id) => long.TryParse(id.StartsWith("fm_") ? id.Substring(3) : "", out long n) && n >= 15112541671L && n <= 15112541677L;
        public static int SynergyHealth(UnitState unit) => CardCatalog.Get(unit.definitionId).name == "전설의기사" ? unit.equipment.Count(x => x.definitionId == "fm_4031940424") : 0;
        public static int SynergyCoins(UnitState unit) => CardCatalog.Get(unit.definitionId).name == "흉성의군주" ? unit.equipment.Count(x => x.definitionId == "fm_13061941464") : 0;
    }
}
