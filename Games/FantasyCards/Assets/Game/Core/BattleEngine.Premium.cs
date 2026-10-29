using System;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        // These entry points consume the actual engine state. They do not roll
        // presentation dice or infer mechanics from a rarity label.
        static int PremiumCode(UnitState unit) => RenewalCardSkills.AttackCode(unit.definitionId);
        static void PremiumAttack(UnitState unit, int value)
        {
            value = Math.Max(0, value);
            if (unit.coinsRolled) unit.rolledAttack = value;
            else unit.bonusPower += value - CardCatalog.BaseAttack(unit);
        }
        static void PremiumDefense(UnitState unit, int value)
        {
            value = Math.Max(0, value);
            if (unit.coinsRolled) unit.rolledDefense = value;
            else unit.armor += value - CardCatalog.BaseDefense(unit);
        }
        static void PremiumHealth(UnitState unit, int value)
        {
            value = Math.Max(0, value) + unit.damage;
            if (unit.coinsRolled) unit.rolledHealth = value;
            else unit.bonusHealth += value - CardCatalog.BaseHealth(unit);
        }
        bool PremiumMonoProduction(int owner, Element element)
        {
            var producing = State.players[owner].terrain.Select(c => CardCatalog.Get(c.definitionId)).Where(c => c.producedColored > 0).ToArray();
            return producing.Length > 0 && producing.All(c => c.element == element);
        }
        void PremiumNotice(UnitState source, int owner, UnitState target, int other, string text, int amount = 0)
        {
            Log("skill", CardCatalog.Get(source.definitionId).name + " · " + text, amount, owner,
                source: source, sourceOwner: owner, target: target, targetOwner: other);
        }
        bool ApplyPremiumDefense(UnitState defender, UnitState attacker, int defenderOwner, int attackerOwner)
        {
            if (PremiumCode(defender) != 489) return false;
            // Source card explicitly caps the late manual's general formula.
            int limit = PremiumMonoProduction(defenderOwner, Element.Dawn) ? 6 : 3;
            int damage = Math.Min(limit, Math.Abs(CardCatalog.UnitAttack(attacker) - State.players[defenderOwner].hand.Count));
            attacker.damage += damage;
            PremiumNotice(defender, defenderOwner, attacker, attackerOwner, "영광의기사 · 선제 HP 피해 " + damage, damage);
            return CardCatalog.UnitHealth(attacker) <= 0;
        }
        int ApplyPremiumBeforeStrike(UnitState source, UnitState target, int owner, int other)
        {
            int code = PremiumCode(source), ap = CardCatalog.UnitAttack(source);
            if (code == 425 && ap >= 3)
            {
                PremiumAttack(source, ap - 3);
                foreach (var enemy in State.players[other].units) PremiumAttack(enemy, (CardCatalog.UnitAttack(enemy) + 1) / 2);
                PremiumNotice(source, owner, target, other, "아이스브레스 · AP -3 / 적 전체 AP 절반(올림)");
            }
            if (code == 219 && ap >= 2)
            {
                PremiumAttack(source, ap - 2);
                foreach (var ally in State.players[owner].units.Where(u => u != source))
                {
                    ally.permanentPower++;
                    if (ally.coinsRolled) ally.rolledAttack++;
                }
                PremiumNotice(source, owner, target, other, "모랄라이징 · 다른 아군 AP 영구 +1");
            }
            if (code == 392 && ap >= 1)
            {
                int empty = State.rules.unitLanes * 2 - State.players.Sum(p => p.units.Count + p.preparedSpells.Count);
                foreach (var enemy in State.players[other].units.Where(u => u.lane > target.lane)) PremiumDefense(enemy, CardCatalog.UnitDefense(enemy) - empty);
                PremiumNotice(source, owner, target, other, "쉐이킹 · 다음 적 전열 DP -" + empty, empty);
            }
            if (code == 755 && ap >= 1)
            {
                foreach (var ally in State.players[owner].units) ally.coinChanceDelta = Math.Min(100, ally.coinChanceDelta + 20);
                foreach (var enemy in State.players[other].units) enemy.coinChanceDelta = Math.Max(-100, enemy.coinChanceDelta - 20);
                PremiumNotice(source, owner, target, other, "행운훔치기 · 아군 코인률 +20% / 적 -20%", 20);
            }
            if (code == 123)
            {
                int count = State.players[owner].terrain.Count(c => CardCatalog.Get(c.definitionId).element == Element.Earth);
                int increase = Math.Max(0, count / 3 - (PremiumMonoProduction(owner, Element.Earth) ? 0 : 1));
                PremiumHealth(source, CardCatalog.UnitHealth(source) + increase);
                PremiumNotice(source, owner, target, other, "대지의왕 · 추가 공격/HP +" + increase, increase);
                return increase; // Explicitly bonus damage, even when AP == 0.
            }
            return 0;
        }
        static bool PremiumIgnoresDefense(string id) => RenewalCardSkills.AttackCode(id) == 52;
        void AfterPremiumStrike(UnitState source, UnitState target, int owner, int other, int targetHpBefore, int damage)
        {
            int code = PremiumCode(source), ap = CardCatalog.UnitAttack(source);
            if (code == 52 && ap >= 1)
            {
                int loss = (ap + 1) / 2;
                foreach (var enemy in State.players[other].units.Where(u => u.lane == 0 || u.lane == 1)) PremiumDefense(enemy, CardCatalog.UnitDefense(enemy) - loss);
                PremiumNotice(source, owner, target, other, "관통무시 · 적 1·2전열 DP -" + loss, loss);
            }
            if (code == 603 && ap >= 1 && (CardCatalog.Get(target.definitionId).capabilityMask & 4) != 0)
            {
                PremiumAttack(target, 0); PremiumDefense(target, 0);
                // A unit already killed by this strike is not resurrected.
                if (CardCatalog.UnitHealth(target) > 0) PremiumHealth(target, 1);
                PremiumNotice(source, owner, target, other, "주문제압 · 공격 후 마법 능력 상대 AP/DP=0, HP=1");
            }
            bool killed = targetHpBefore > 0 && CardCatalog.UnitHealth(target) <= 0;
            if (!killed) return;
            if (code == 539)
            {
                var next = State.players[other].units.Where(u => u.lane > target.lane && CardCatalog.UnitHealth(u) > 0).OrderBy(u => u.lane).FirstOrDefault();
                if (next != null)
                {
                    int physical = Math.Max(0, ap - targetHpBefore - CardCatalog.UnitDefense(next)); next.damage += physical;
                    PremiumNotice(source, owner, next, other, "이블피어싱 · 다음 적 물리 피해 " + physical, physical);
                }
            }
            if (code == 731)
            {
                foreach (var enemy in State.players[other].units.Where(u => CardCatalog.UnitHealth(u) > 0)) PremiumAttack(enemy, CardCatalog.UnitAttack(enemy) - 2);
                PremiumNotice(source, owner, target, other, "포효공격 · 적 전체 AP -2", 2);
            }
            if (code == 397)
            {
                int lane = PremiumInsertSlot(owner, source.lane + 1);
                if (lane >= 0)
                {
                    var generated = CreateGeneratedCard(owner, "fm_2050330032", source.instanceId, source.definitionId, "premium-stone");
                    var spell = new PreparedSpellState { instanceId = generated.instanceId, definitionId = generated.definitionId, lane = lane };
                    State.players[owner].preparedSpells.Add(spell);
                    PremiumNotice(source, owner, null, owner, "스톤드로잉 · 다음 전열에 낙석 생성");
                    // Combat's spell snapshot already resolved: the new spell
                    // waits for the next combat instead of activating instantly.
                }
            }
        }
        int PremiumInsertSlot(int owner, int desired)
        {
            if (desired < 0 || desired >= State.rules.unitLanes) return -1;
            var p = State.players[owner]; int empty = -1;
            for (int lane = desired; lane < State.rules.unitLanes; lane++)
                if (p.AtLane(lane) == null && p.SpellAtLane(lane) == null) { empty = lane; break; }
            if (empty < 0) return -1;
            for (int lane = empty - 1; lane >= desired; lane--)
            {
                var unit = p.AtLane(lane); var spell = p.SpellAtLane(lane);
                if (unit != null) unit.lane++;
                if (spell != null) spell.lane++;
            }
            return desired;
        }
        void OnPremiumDeath(UnitState source, int owner)
        {
            int code = PremiumCode(source), other = 1 - owner; var p = State.players[owner];
            if (code == 417)
            {
                int dp = CardCatalog.UnitDefense(source);
                var ally = p.units.Where(u => !u.exhausted && CardCatalog.UnitHealth(u) > 0).OrderBy(u => u.lane).FirstOrDefault();
                var enemy = State.players[other].units.Where(u => !u.exhausted && CardCatalog.UnitHealth(u) > 0).OrderBy(u => u.lane).FirstOrDefault();
                if (ally != null) PremiumDefense(ally, CardCatalog.UnitDefense(ally) + dp);
                if (enemy != null) PremiumAttack(enemy, CardCatalog.UnitAttack(enemy) - dp);
                PremiumNotice(source, owner, ally, owner, "실드오오라 · 다음 행동가능 아군 DP/적 AP " + dp, dp);
            }
            if (code == 674)
            {
                var enemy = State.players[other].units.Where(u => !u.exhausted && CardCatalog.UnitHealth(u) > 0).OrderBy(u => u.lane).FirstOrDefault();
                if (enemy != null)
                {
                    int physical = Math.Max(0, CardCatalog.UnitAttack(source) - CardCatalog.UnitDefense(enemy)); enemy.damage += physical;
                    RecordPolishLethalSource(source, enemy);
                    PremiumNotice(source, owner, enemy, other, "크로스카운트 · 다음 행동가능 적 물리 피해 " + physical, physical);
                }
            }
            if (code == 669)
            {
                int heads = State.events.Count(e => e.turn == State.turn && e.kind == "coin" && e.sourceInstanceId == source.instanceId && e.coinHeads);
                int enemyBattleUnit = combatUnitIds[other];
                foreach (var enemy in State.players[other].units.Where(u => CardCatalog.UnitHealth(u) > 0 && u.instanceId != enemyBattleUnit)) { enemy.damage += heads; RecordPolishLethalSource(source, enemy); }
                PremiumNotice(source, owner, null, other, "코인플레임 · 전투 상대 외 적 HP 피해 " + heads, heads);
            }
            if (code == 473)
            {
                var copied = State.players[other].hand.FirstOrDefault(c => CardCatalog.Get(c.definitionId).kind == CardKind.Unit && CardCatalog.Get(c.definitionId).level <= CardCatalog.Get(source.definitionId).level);
                if (copied != null)
                {
                    int lane = PremiumInsertSlot(owner, source.lane);
                    if (lane >= 0)
                    {
                        var generated = CreateGeneratedCard(owner, copied.definitionId, source.instanceId, source.definitionId, "premium-echo");
                        var unit = new UnitState { instanceId = generated.instanceId, definitionId = generated.definitionId, lane = lane };
                        p.units.Add(unit); PremiumNotice(source, owner, unit, owner, "에코카피 · 상대 손패 유닛 복사");
                    }
                }
            }
            if (code == 518)
            {
                int lane = PremiumInsertSlot(owner, source.lane);
                if (lane >= 0)
                {
                    var generated = CreateGeneratedCard(owner, "fm_2042920052", source.instanceId, source.definitionId, "premium-wood");
                    var unit = new UnitState { instanceId = generated.instanceId, definitionId = generated.definitionId, lane = lane };
                    p.units.Add(unit); PremiumNotice(source, owner, unit, owner, "워킹우드 · 트리언트 소환");
                }
                if (p.terrain.Count > 0)
                {
                    var land = p.terrain[0]; p.terrain.RemoveAt(0); p.discard.Add(land);
                    Log("skill", "워킹우드 · 아군 지형 1 파괴", 1, owner, source: source, sourceOwner: owner, targetCard: land, targetOwner: owner);
                }
            }
        }
    }
}
