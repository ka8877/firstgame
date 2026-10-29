using System;
using System.Linq;
namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        // The late official manual explicitly distinguishes bonus attack damage
        // from AP. This method never turns a +damage technique into permanent AP.
        int RenewalStrike(UnitState source, UnitState target, int owner, int other, string kind)
        {
            if (ApplyPremiumDefense(target, source, other, owner)) return 0;
            int code = RenewalCardSkills.AttackCode(source.definitionId);
            int originalAp = CardCatalog.UnitAttack(source), originalDp = CardCatalog.UnitDefense(source), originalHp = CardCatalog.UnitHealth(source);
            int ap = originalAp, extra = 0;
            int targetAp = CardCatalog.UnitAttack(target), targetDp = CardCatalog.UnitDefense(target), targetHp = CardCatalog.UnitHealth(target);
            bool active = ap >= 1, ignoreDefense = false;
            int ownLevel = CardCatalog.Get(source.definitionId).level, otherLevel = CardCatalog.Get(target.definitionId).level;
            int trait = CardCatalog.Get(target.definitionId).capabilityMask;
            // Existing exact original technique also activates when this unit
            // makes a surviving counterattack, not merely on its attacking turn.
            if (source.definitionId == RestorationCardSkills.StrongPoison && active)
            { target.rolledAttack = 1; target.rolledHealth = target.damage + 1; Log("skill", "정글의독침수 · 강한치명 · 상대 AP·HP =1", 1, owner, source: source, sourceOwner: owner, target: target, targetOwner: other); }
            switch (code)
            {
                case 9: if (ap >= 2) { source.rolledAttack = Math.Max(0, ap - 2); source.rolledDefense++; source.rolledHealth++; } break;
                case 11: if (otherLevel <= ownLevel) extra = 2; break;
                case 42: if (active) { extra = targetAp - ap; ignoreDefense = true; } break;
                case 49: if (otherLevel <= ownLevel) extra = 1; break;
                case 106: if (active) target.rolledAttack = Math.Max(0, targetAp - 2); break;
                case 135: extra = originalDp; break;
                case 136:
                    if (active)
                        foreach (var enemy in State.players[other].units)
                        {
                            if (enemy.coinsRolled) { enemy.rolledAttack = Math.Max(0, enemy.rolledAttack - targetDp); enemy.rolledDefense = Math.Max(0, enemy.rolledDefense - targetDp); }
                            else { enemy.bonusPower -= targetDp; enemy.armor -= targetDp; }
                        }
                    break;
                case 188: if (active && (trait & 4) != 0) extra = 2; break;
                case 224: if (active && (trait & 2) != 0) { target.rolledAttack = 0; target.rolledDefense = 0; } break;
                case 243: ignoreDefense = active; break;
                case 245: if (active) { extra = targetDp; ignoreDefense = true; } break;
                case 249: if (active) target.rolledDefense = 0; break;
                case 339: if (active) { target.rolledAttack = 0; target.rolledDefense++; } break;
                case 365: if (active) extra = CardCatalog.Get(target.definitionId).genericCost; break;
                case 447: if (active && (trait & 1) != 0) extra = 2; break;
                case 450: if (active && (trait & 1) != 0) { target.rolledAttack = 0; target.rolledDefense = 0; } break;
                case 454: if (active) { target.rolledAttack = Math.Max(0, targetAp - 1); target.rolledDefense = Math.Max(0, targetDp - 1); } break;
                case 482: if (active) { source.rolledAttack = Math.Max(0, ap - 1); extra = source.rolledAttack; } break;
                case 508: if (active && otherLevel >= ownLevel) extra = 1; break;
                case 533: source.rolledDefense++; break;
                case 570: if (active && (trait & 2) != 0) extra = 2; break;
                case 572: source.rolledHealth++; break;
                case 643: if (active) target.rolledHealth = target.damage + 1; break;
                case 767: if (active) { target.rolledAttack = targetHp; target.rolledHealth = target.damage + targetAp; } break;
                // Long range, curse and vampire have post-strike conditions.
            }
            extra += ApplyPremiumBeforeStrike(source, target, owner, other);
            ignoreDefense |= PremiumIgnoresDefense(source.definitionId);
            if (code != 0 && code != 489 && (extra != 0 || ignoreDefense || CardCatalog.UnitAttack(source) != originalAp || CardCatalog.UnitDefense(source) != originalDp || CardCatalog.UnitHealth(source) != originalHp || CardCatalog.UnitAttack(target) != targetAp || CardCatalog.UnitDefense(target) != targetDp || CardCatalog.UnitHealth(target) != targetHp) && !RenewalPremiumCode(code))
                Log("skill", CardCatalog.Get(source.definitionId).name + " · " + CardCatalog.Get(source.definitionId).text,
                    extra, owner, source: source, sourceOwner: owner, target: target, targetOwner: other,
                    hasStatTransition: true, beforeAttack: originalAp, beforeDefense: originalDp, beforeHealth: originalHp, additionalAttackDamage: extra);
            bool evaded = RestorationCardSkills.EvadesBeast(target, source);
            int incoming = Math.Max(0, CardCatalog.UnitAttack(source) + extra - (ignoreDefense ? 0 : CardCatalog.UnitDefense(target)));
            int damage = evaded ? 0 : incoming;
            if (evaded) Log("skill", "투우사 · 야수회피 · 공격 피해 무효", 0, other, source: target, sourceOwner: other, target: source, targetOwner: owner);
            int hpBeforeDamage = CardCatalog.UnitHealth(target);
            target.damage += damage;
            Log(kind, CardCatalog.Get(source.definitionId).name + " → " + CardCatalog.Get(target.definitionId).name + " · 피해 " + damage,
                damage, owner, source: source, sourceOwner: owner, target: target, targetOwner: other, additionalAttackDamage: extra);
            if (active && code == 576) { target.rolledAttack = Math.Max(0, CardCatalog.UnitAttack(target) - 2); target.rolledDefense = Math.Max(0, CardCatalog.UnitDefense(target) - 2); }
            if (active && code == 788 && damage > 0) source.rolledHealth += (Math.Min(hpBeforeDamage, damage) + 1) / 2;
            if ((active && code == 576) || (active && code == 788 && damage > 0))
                Log("skill", CardCatalog.Get(source.definitionId).name + " · 공격 후 기술", damage, owner, source: source, sourceOwner: owner, target: target, targetOwner: other);
            AfterPremiumStrike(source, target, owner, other, hpBeforeDamage, damage);
            return damage;
        }

        static bool RenewalPremiumCode(int code) => new[] { 489, 425, 219, 392, 755, 123, 52, 603, 539, 731, 397, 417, 674, 669, 473, 518 }.Contains(code);

        void ApplyRenewalEquipment(int owner, UnitState unit, CardInstance item)
        {
            var p = State.players[owner];
            if (RenewalCardSkills.DoubleChain(item.definitionId))
            {
                // Apply the build trigger only to the actual paid equip action;
                // its generated item must not recursively chain itself.
                var next = p.units.OrderBy(u => u.lane).FirstOrDefault(u => u != unit && u.equipment.Count == 0);
                if (next != null)
                {
                    var copy = CreateGeneratedCard(owner, item.definitionId, item.instanceId, item.definitionId, "double-chain"); next.equipment.Add(copy);
                    Log("skill", CardCatalog.Get(item.definitionId).name + " · 더블체인 · 미장착 아군에 동일 아이템 장착", 1, owner,
                        card: item, sourceOwner: owner, lane: unit.lane, target: next, targetOwner: owner);
                }
            }
            if (RenewalCardSkills.IdCopy(item.definitionId) && CardCatalog.Get(unit.definitionId).level <= 2)
            {
                // Preserve field order: find a real vacancy after the equipped
                // unit, shifting occupied lanes only when a vacancy exists.
                int empty = -1;
                for (int lane = unit.lane + 1; lane < State.rules.unitLanes; lane++)
                    if (p.AtLane(lane) == null && p.SpellAtLane(lane) == null) { empty = lane; break; }
                if (empty >= 0)
                {
                    for (int lane = empty - 1; lane > unit.lane; lane--)
                    { var occupant = p.AtLane(lane); if (occupant != null) occupant.lane++; var spell = p.SpellAtLane(lane); if (spell != null) spell.lane++; }
                    var copy = unit.Copy(); copy.instanceId = CreateGeneratedCard(owner, unit.definitionId, item.instanceId, item.definitionId, "id-copy").instanceId; copy.lane = unit.lane + 1; copy.equipment.Clear(); p.units.Add(copy);
                    Log("skill", CardCatalog.Get(item.definitionId).name + " · 이드카피 · 장비 없이 동일 유닛 복사", 1, owner,
                        card: item, sourceOwner: owner, lane: unit.lane, target: copy, targetOwner: owner);
                }
            }
        }
    }
}
