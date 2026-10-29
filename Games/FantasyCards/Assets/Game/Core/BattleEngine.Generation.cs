using System;
using System.Collections.Generic;
using System.Linq;
namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        static IEnumerable<CardInstance> AllInstances(PlayerState p) => p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard)
            .Concat(p.units.Select(u => new CardInstance(u.instanceId, u.definitionId))).Concat(p.units.SelectMany(u => u.equipment))
            .Concat(p.preparedSpells.Select(u => new CardInstance(u.instanceId, u.definitionId)));
        CardInstance CreateGeneratedCard(int owner, string definitionId, int sourceInstanceId, string sourceDefinitionId, string reason)
        {
            if (State.generatedCards.Count >= 4096) throw new InvalidOperationException("생성 카드 수가 안전 한도를 넘었습니다.");
            var card = new CardInstance(State.nextInstanceId++, definitionId);
            State.generatedCards.Add(new GeneratedCardProof { instanceId = card.instanceId, definitionId = definitionId,
                sourceInstanceId = sourceInstanceId, sourceDefinitionId = sourceDefinitionId, owner = owner, reason = reason });
            return card;
        }
        static void ValidateGeneratedCards(BattleState state, int firstGenerated)
        {
            var instances = state.players.SelectMany((p, owner) => AllInstances(p).Select(c => new { card = c, owner })).ToDictionary(x => x.card.instanceId);
            var seen = new HashSet<int>();
            foreach (var proof in state.generatedCards ?? new List<GeneratedCardProof>())
            {
                if (proof == null || proof.instanceId < firstGenerated || !seen.Add(proof.instanceId)
                    || proof.sourceInstanceId <= 0 || proof.sourceInstanceId >= proof.instanceId || proof.owner < 0 || proof.owner > 1
                    || !instances.TryGetValue(proof.instanceId, out var current) || !instances.TryGetValue(proof.sourceInstanceId, out var source)
                    || current.card.definitionId != proof.definitionId || source.card.definitionId != proof.sourceDefinitionId
                    || current.owner != proof.owner || source.owner != proof.owner)
                    throw new ArgumentException("생성 카드와 원본·소유자 출처가 일치하지 않습니다.");
                var definition = CardCatalog.Get(proof.definitionId); int code = RenewalCardSkills.AttackCode(proof.sourceDefinitionId);
                bool allowed = proof.reason == "double-chain" && RenewalCardSkills.DoubleChain(proof.sourceDefinitionId) && proof.definitionId == proof.sourceDefinitionId
                    || proof.reason == "id-copy" && RenewalCardSkills.IdCopy(proof.sourceDefinitionId) && definition.kind == CardKind.Unit && definition.level <= 2
                    || proof.reason == "premium-stone" && code == 397 && proof.definitionId == "fm_2050330032"
                    || proof.reason == "premium-echo" && code == 473 && definition.kind == CardKind.Unit && definition.level <= CardCatalog.Get(proof.sourceDefinitionId).level
                    || proof.reason == "premium-wood" && code == 518 && proof.definitionId == "fm_2042920052";
                if (!allowed) throw new ArgumentException("원본 기술에서 지원하지 않는 카드 생성입니다.");
            }
            if (!seen.SetEquals(instances.Keys.Where(id => id >= firstGenerated))) throw new ArgumentException("생성 카드 출처 기록이 누락되었습니다.");
        }
    }
}
