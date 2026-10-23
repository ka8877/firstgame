using System.Linq;

namespace FantasyCards.Core
{
    public static class BattleAi
    {
        public static int ChooseBlocker(BattleState state, int defender)
        {
            var attacker = state.players[state.activePlayer].units.Find(x => x.instanceId == state.pendingAttackerId);
            if (attacker == null) return -1;
            var candidates = state.players[defender].units.Where(x => !x.exhausted && !x.summoningSick).OrderByDescending(x => CardCatalog.Get(x.definitionId).health - x.damage + x.armor).ThenBy(x => x.lane).ToList();
            return candidates.Count == 0 ? -1 : candidates[0].instanceId;
        }
        // A single decision per call lets presentation animate committed events before continuing.
        public static bool Step(BattleEngine engine, int player, out string reason)
        {
            reason = ""; var s = engine.State;
            if (s.phase == BattlePhase.Complete) return false;
            if (s.phase == BattlePhase.BlockResponse) return s.activePlayer != player && engine.ResolveBlock(player, ChooseBlocker(s, player), out reason);
            if (s.activePlayer != player) return false;
            var p = s.players[player];
            if (s.phase == BattlePhase.Main)
            {
                foreach (var card in p.hand.OrderBy(c => CardCatalog.Get(c.definitionId).kind).ThenBy(c => CardCatalog.Get(c.definitionId).cost).ThenBy(c => c.instanceId))
                {
                    var d = CardCatalog.Get(card.definitionId);
                    for (int lane = 0; lane < s.rules.unitLanes; lane++)
                    {
                        if (d.effect == Effect.Heal && (p.AtLane(lane) == null || p.AtLane(lane).damage == 0)) continue;
                        if (engine.CanPlay(player, card.instanceId, lane, out _)) return engine.Play(player, card.instanceId, lane, out reason);
                    }
                }
                return engine.BeginCombat(player, out reason);
            }
            var attacker = p.units.Where(x => !x.exhausted && !x.summoningSick).OrderByDescending(x => CardCatalog.Get(x.definitionId).attack + x.bonusPower).ThenBy(x => x.lane).FirstOrDefault();
            if (attacker == null) return engine.EndTurn(player, out reason);
            return engine.DeclareAttack(player, attacker.instanceId, -1, out reason);
        }
    }
}
