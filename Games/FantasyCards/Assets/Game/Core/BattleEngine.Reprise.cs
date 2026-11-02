using System.Linq;

namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        // The late official FAQ 734 explicitly permits dragging field cards
        // into a different order during either preparation. Units and reserved
        // spells share the same five slots; an occupied destination swaps them.
        public bool CanMoveFieldCard(int player, int instanceId, int destinationLane, out string reason)
        {
            reason = "";
            if (!IsPreparing(player)) return Fail("자신의 준비 단계에서만 전열을 바꿀 수 있습니다.", out reason);
            if (State.openingHandOpen) return Fail("시작 손패를 먼저 확정하세요.", out reason);
            if (destinationLane < 0 || destinationLane >= State.rules.unitLanes) return Fail("이동할 전열 칸을 선택하세요.", out reason);
            var p = State.players[player];
            var unit = p.units.Find(u => u.instanceId == instanceId);
            var spell = p.preparedSpells.Find(s => s.instanceId == instanceId);
            if (unit == null && spell == null) return Fail("전열에 놓인 아군 유닛이나 예고 마법을 선택하세요.", out reason);
            int lane = unit != null ? unit.lane : spell.lane;
            if (lane == destinationLane) return Fail("이미 선택한 전열 칸에 있습니다.", out reason);
            return true;
        }

        public bool MoveFieldCard(int player, int instanceId, int destinationLane, out string reason)
        {
            if (!CanMoveFieldCard(player, instanceId, destinationLane, out reason)) return false;
            var p = State.players[player];
            var unit = p.units.Find(u => u.instanceId == instanceId);
            var spell = p.preparedSpells.Find(s => s.instanceId == instanceId);
            int previousLane = unit != null ? unit.lane : spell.lane;
            var replacedUnit = p.AtLane(destinationLane);
            var replacedSpell = p.SpellAtLane(destinationLane);
            if (replacedUnit != null) replacedUnit.lane = previousLane;
            if (replacedSpell != null) replacedSpell.lane = previousLane;
            if (unit != null) unit.lane = destinationLane; else spell.lane = destinationLane;
            string id = unit != null ? unit.definitionId : spell.definitionId;
            var replacedCard = replacedUnit != null ? new CardInstance(replacedUnit.instanceId, replacedUnit.definitionId)
                : replacedSpell != null ? new CardInstance(replacedSpell.instanceId, replacedSpell.definitionId) : null;
            // A reserved spell remains hidden in the public log until reveal.
            // Its chosen effect target is a field slot, not a movable unit ID.
            Log("reposition", Owner(player) + " · " + (unit != null ? CardCatalog.Get(id).name : "예고 마법") +
                " 전열 " + (previousLane + 1) + " → " + (destinationLane + 1) + (replacedCard == null ? " 이동" : " 교환"),
                replacedCard == null ? 0 : 1, player, card: new CardInstance(instanceId, id), lane: previousLane,
                sourceOwner: player, targetOwner: player, targetCard: replacedCard, destinationLane: destinationLane);
            return true;
        }

        // The official offline rules allow disposal before READY, except for
        // spent terrain. Optional per-instance records now distinguish unused
        // production; older unknown records stay locked until a soul refresh.
        public bool CanDiscardField(int player, int instanceId, out string reason)
        {
            reason = "";
            if (!IsPreparing(player)) return Fail("자신의 준비 단계에서만 필드 카드를 소각할 수 있습니다.", out reason);
            if (State.openingHandOpen) return Fail("시작 손패를 먼저 확정하세요.", out reason);
            var p = State.players[player];
            var terrain = p.terrain.Find(c => c.instanceId == instanceId);
            if (terrain != null) return CanDiscardTerrain(p, terrain, out reason);
            if (!p.units.Any(u => u.instanceId == instanceId || u.equipment.Any(c => c.instanceId == instanceId)) &&
                !p.preparedSpells.Any(s => s.instanceId == instanceId))
                return Fail("필드의 아군 지형·유닛·장착 아이템·예고 마법을 선택하세요.", out reason);
            return true;
        }

        public bool DiscardField(int player, int instanceId, out string reason)
        {
            if (!CanDiscardField(player, instanceId, out reason)) return false;
            var p = State.players[player];
            var terrain = p.terrain.Find(c => c.instanceId == instanceId);
            if (terrain != null) { DiscardTerrain(player, terrain); CheckVictory(); return true; }
            var unit = p.units.Find(u => u.instanceId == instanceId);
            if (unit != null) RemoveUnitToDiscard(player, unit, "discard-field");
            else
            {
                var spell = p.preparedSpells.Find(s => s.instanceId == instanceId);
                if (spell != null)
                {
                    p.preparedSpells.Remove(spell);
                    var card = new CardInstance(spell.instanceId, spell.definitionId); p.discard.Add(card);
                    Log("discard-field", Owner(player) + " · 예고 마법 1장 소각", 1, player,
                        card: card, sourceOwner: player, lane: spell.lane);
                }
                else
                {
                    var bearer = p.units.Find(u => u.equipment.Any(c => c.instanceId == instanceId));
                    var card = bearer.equipment.Find(c => c.instanceId == instanceId);
                    bearer.equipment.Remove(card); p.discard.Add(card);
                    Log("discard-field", CardCatalog.Get(card.definitionId).name + " · 장착 아이템 소각", 1, player,
                        card: card, sourceOwner: player, lane: bearer.lane, target: bearer, targetOwner: player);
                }
            }
            // Taking away an HP-granting item can destroy its bearer. Apply the
            // same destruction chains rather than leaving an invalid live unit.
            RemoveDefeated(); CheckVictory();
            return true;
        }
    }
}
