using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        // Transient combat attribution, never a fabricated presentation roll or
        // serialized source of truth. It also covers collateral strike damage.
        readonly Dictionary<int, string> polishLethalSources = new Dictionary<int, string>();

        void RecordPolishLethalSource(UnitState source, UnitState target)
        {
            if (source != null && target != null && CardCatalog.UnitHealth(target) <= 0)
                polishLethalSources[target.instanceId] = source.definitionId;
        }

        void RecordPolishStrikeDeaths(UnitState source, int other, HashSet<int> livingBefore)
        {
            foreach (var target in State.players[other].units)
                if (livingBefore.Contains(target.instanceId)) RecordPolishLethalSource(source, target);
        }

        int PolishDefenseReduction(UnitState defender, UnitState attacker, int owner, int incoming)
        {
            int code = PolishCardSkills.DefenseCode(defender.definitionId), reduction = 0;
            if (code == 662)
                reduction = State.events.Count(e => e.turn == State.turn && e.kind == "coin" && e.sourceInstanceId == defender.instanceId && e.coinHeads);
            if (code == 18 && CardCatalog.Get(attacker.definitionId).level <= CardCatalog.Get(defender.definitionId).level)
                reduction = 1;
            if (code == 166)
                reduction = (State.rules.unitLanes - State.players[owner].units.Count - State.players[owner].preparedSpells.Count) / 2;
            reduction = Math.Min(incoming, Math.Max(0, reduction));
            if (reduction > 0)
                PremiumNotice(defender, owner, attacker, 1 - owner, "방어 기술 · 피해 " + reduction + " 감소", reduction);
            return reduction;
        }

        void QueuePolishSouls(UnitState source, int owner, CardDefinition recovered)
        {
            var p = State.players[owner];
            if (p.nextColoredSoulBonus == null) p.nextColoredSoulBonus = new int[7];
            int index = (int)recovered.element;
            p.nextColoredSoulBonus[index] = Math.Min(State.rules.maximumResource, p.nextColoredSoulBonus[index] + recovered.coloredCost);
            p.nextGenericSoulBonus = Math.Min(State.rules.maximumResource, p.nextGenericSoulBonus + recovered.cost);
            PremiumNotice(source, owner, null, owner, "소울 회수 예약 · " + CardCatalog.ElementName(recovered.element) + " " + recovered.coloredCost + " / 총소울 " + recovered.cost, recovered.cost);
        }

        void PolishBeforeStrike(UnitState source, UnitState target, int owner, int other)
        {
            int code = PremiumCode(source);
            if (code == 666 && CardCatalog.UnitAttack(source) >= 1)
            {
                var coins = State.events.Where(e => e.turn == State.turn && e.kind == "coin" && e.sourceInstanceId == source.instanceId).ToArray();
                if (coins.Length > 0 && coins.All(e => !e.coinHeads))
                {
                    PremiumAttack(target, 0);
                    PremiumNotice(source, owner, target, other, "코인예측 · 전부 뒷면인 실제 코인 / 적 AP=0");
                }
            }
            if (code == 374 && CardCatalog.UnitAttack(source) >= 1)
            {
                var p = State.players[owner]; var element = CardCatalog.Get(source.definitionId).element;
                if (p.nextColoredSoulBonus == null) p.nextColoredSoulBonus = new int[7];
                p.nextColoredSoulBonus[(int)element] = Math.Min(State.rules.maximumResource, p.nextColoredSoulBonus[(int)element] + 1);
                p.nextGenericSoulBonus = Math.Min(State.rules.maximumResource, p.nextGenericSoulBonus + 1);
                PremiumNotice(source, owner, null, owner, "솔생성 · 다음 소울 코스트 속성 소울 +1", 1);
            }
            if (code == 179 && CardCatalog.UnitAttack(source) >= 1 && source.equipment.Count < 2)
            {
                var p = State.players[owner];
                var item = p.discard.LastOrDefault(c => CardCatalog.Get(c.definitionId).kind == CardKind.Equipment);
                if (item == null) return;
                var definition = CardCatalog.Get(item.definitionId);
                if (definition.requiredCapabilityMask != 0 && (definition.requiredCapabilityMask & CardCatalog.Get(source.definitionId).capabilityMask) == 0) return;
                int oldAp = CardCatalog.UnitAttack(source), oldDp = CardCatalog.UnitDefense(source), oldHp = CardCatalog.UnitHealth(source);
                p.discard.Remove(item); source.equipment.Add(item);
                // Coins already happened. Restore only the printed stat change
                // now; the actual restored item supplies future coin modifiers.
                if (source.coinsRolled)
                {
                    source.rolledAttack = Math.Max(0, oldAp + definition.attack);
                    source.rolledDefense = Math.Max(0, oldDp + definition.defense);
                    source.rolledHealth = Math.Max(0, oldHp + definition.health) + source.damage;
                }
                PremiumNotice(source, owner, source, owner, "리크리에이트 · 소각장 아이템 재장착");
            }
        }

        void PolishAfterStrike(UnitState source, UnitState target, int owner, int other, int targetHpBefore)
        {
            if (PremiumCode(source) == 368 && targetHpBefore > 0 && CardCatalog.UnitHealth(target) <= 0)
                QueuePolishSouls(source, owner, CardCatalog.Get(target.definitionId));
        }

        void OnPolishDeath(UnitState source, int owner)
        {
            int code = PremiumCode(source); var p = State.players[owner];
            if (code == 371) QueuePolishSouls(source, owner, CardCatalog.Get(source.definitionId));
            if (code == 493)
            {
                // Top of the discard is the last appended card. Move existing
                // instances, including a just-discarded unit, without cloning.
                var recovered = p.discard.Where(c => CardCatalog.Get(c.definitionId).kind == CardKind.Unit)
                    .Reverse().Take(Math.Min(2, State.rules.handLimit - p.hand.Count)).ToArray();
                foreach (var card in recovered) { p.discard.Remove(card); p.hand.Add(card); }
                PremiumNotice(source, owner, null, owner, "영혼의사슬 · 소각장 유닛 " + recovered.Length + "장 손패 복귀", recovered.Length);
            }
            if (code == 131 && polishLethalSources.TryGetValue(source.instanceId, out string killerId)
                && CardCatalog.Get(killerId).level < CardCatalog.Get(source.definitionId).level)
            {
                int lane = PremiumInsertSlot(owner, source.lane);
                if (lane >= 0)
                {
                    var card = CreateGeneratedCard(owner, killerId, source.instanceId, source.definitionId, "polish-death-mirror");
                    var copy = new UnitState { instanceId = card.instanceId, definitionId = card.definitionId, lane = lane };
                    p.units.Add(copy);
                    PremiumNotice(source, owner, copy, owner, "데스미로잉 · 자신보다 낮은 레벨의 파괴자 복사");
                }
            }
            polishLethalSources.Remove(source.instanceId);
        }
    }
}
