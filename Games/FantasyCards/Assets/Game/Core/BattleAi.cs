using System.Linq;

namespace FantasyCards.Core
{
    public static class BattleAi
    {
        // Only own hand, public units and committed resource totals are inspected.
        // Hidden opposing spells and future coin RNG are never used to select an action.
        public static bool Step(BattleEngine engine, int player, out string reason)
        {
            reason = ""; var s = engine.State;
            if (s.phase == BattlePhase.Complete || !engine.IsPreparing(player)) return false;
            if (s.openingHandOpen) return engine.ConfirmOpeningHand(player, out reason);
            var p = s.players[player];
            foreach (var card in p.hand.OrderBy(c => CardCatalog.Get(c.definitionId).kind).ThenBy(c => CardCatalog.Get(c.definitionId).level).ThenBy(c => c.instanceId))
            {
                var d = CardCatalog.Get(card.definitionId);
                if (d.kind == CardKind.Spell && !HasUsefulSpellTarget(s, player, d)) continue;
                for (int lane = -1; lane < s.rules.unitLanes; lane++)
                {
                    if (d.kind == CardKind.Equipment && (p.AtLane(lane) == null || CardCatalog.UnitHealth(p.AtLane(lane)) + d.health <= 0)) continue;
                    if (engine.CanPlay(player, card.instanceId, lane, out _)) return engine.Play(player, card.instanceId, lane, out reason);
                }
            }
            // A dead hand may be returned once before any card has been used in this preparation.
            if (engine.CanShuffleHand(player, out _)) return engine.ShuffleHand(player, out reason);
            return engine.CompletePreparation(player, out reason);
        }
        static bool HasUsefulSpellTarget(BattleState state, int player, CardDefinition card)
        {
            var own = state.players[player]; var enemy = state.players[1 - player];
            if (card.effect == Effect.StatOperations)
                return card.operations.Any(op => state.players.SelectMany((p, owner) => p.units.Select(u => new { owner, unit = u }))
                    .Any(x => (op.side == OperationSide.Both || (op.side == OperationSide.Ally ? x.owner == player : x.owner != player)) && (op.laneMask & (1 << x.unit.lane)) != 0 && (op.requiredCapabilityMask == 0 || (CardCatalog.Get(x.unit.definitionId).capabilityMask & op.requiredCapabilityMask) != 0) && (op.requiredElement < 0 || (int)CardCatalog.Get(x.unit.definitionId).element == op.requiredElement)));
            switch (card.effect)
            {
                case Effect.Damage: case Effect.Sweep: case Effect.Weaken: case Effect.DefenseBreak: return enemy.units.Count > 0;
                case Effect.Heal: case Effect.HealAll: return own.units.Any(u => u.damage > 0);
                case Effect.Rally: case Effect.Power: case Effect.Armor: case Effect.Health: case Effect.Coin:
                case Effect.RallyAll: case Effect.ArmorAll: case Effect.HealthAll: return own.units.Count > 0;
                case Effect.DrainAll: return own.units.Count + enemy.units.Count > 0;
                case Effect.Draw: return own.deck.Count > 0 && own.hand.Count < state.rules.handLimit;
                default: return true;
            }
        }
        public static int ChooseBlocker(BattleState state, int defender)
        { return state.players[defender].units.OrderBy(u => u.lane).Select(u => u.instanceId).DefaultIfEmpty(-1).First(); }
    }
}
