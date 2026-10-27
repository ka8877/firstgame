using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed class ScenarioStage
    {
        public readonly string id, name, description, objective, recommendedDeckId, encounterId;
        public readonly int tier;
        public int encounterIndex => Array.FindIndex(DemoCatalog.Encounters, encounter => encounter.id == encounterId);
        public ScenarioStage(string id, string name, string description, string objective,
            string recommendedDeckId, string encounterId, int tier)
        {
            this.id = id; this.name = name; this.description = description; this.objective = objective;
            this.recommendedDeckId = recommendedDeckId; this.encounterId = encounterId; this.tier = tier;
        }
    }

    // A newly authored four-match solo learning route. Original-card mechanics
    // and the local CPU are real; this is not a recovered original FM campaign.
    // Stable encounter IDs bind saved battles to stages without a new schema.
    public static class FmScenarioCatalog
    {
        public static readonly ScenarioStage[] Stages =
        {
            new ScenarioStage("roots", "1장 · 소울의 기초", "첫 관문은 숲의 전열입니다. 지형으로 색 소울과 총 소울을 확보하고 낮은 레벨의 유닛부터 전개하세요.", "상대 덱을 소진시켜 승리하세요. LP·소울·손패 셔플을 익힙니다.", "mono-forest", "scenario-roots", 1),
            new ScenarioStage("tides", "2장 · 파도의 시험", "물과 암흑의 마법이 기다립니다. 양측의 준비가 끝나면 낮은 레벨 마법부터 공개됩니다. 방어 전용 마법의 타이밍을 살피세요.", "상대 덱을 소진시켜 승리하세요. 마법 예고와 공방 순서를 익힙니다.", "dual-light-water", "scenario-tides", 2),
            new ScenarioStage("forge", "3장 · 강철의 관문", "장비와 불 마법을 갖춘 상대입니다. 장비·마법·야수의 능력과 유지비를 확인하며 다섯 전열을 지키세요.", "상대 덱을 소진시켜 승리하세요. 장비 조건과 소울 코스트를 익힙니다.", "premium-grove", "scenario-forge", 3),
            new ScenarioStage("relic", "4장 · 유산의 수호자", "대지와 빛의 수호자는 상위 등급 카드를 지녔습니다. 시크릿 차원의충격파와 유니크의 기술을 확인하고 전열을 나누어 배치하세요.", "상대 덱을 소진시켜 승리하세요. 상위 등급의 원문 효과에 대응합니다.", "premium-relic", "scenario-relic", 4)
        };
        public static ScenarioStage Get(string id)
        { return Array.Find(Stages, stage => stage.id == id) ?? throw new ArgumentException("알 수 없는 시나리오: " + id); }
        public static bool IsUnlocked(string id, IEnumerable<string> completedIds)
        {
            var stage = Get(id); int index = Array.IndexOf(Stages, stage);
            var completed = new HashSet<string>(completedIds ?? Enumerable.Empty<string>());
            return index == 0 || Stages.Take(index).All(previous => completed.Contains(previous.id));
        }
        public static bool IsScenarioBattle(ScenarioStage stage, BattleState state)
        {
            if (stage == null || state == null || state.players == null || state.players.Count != 2 || stage.encounterIndex < 0) return false;
            var encounter = DemoCatalog.GetEncounter(stage.encounterIndex);
            return state.encounterIndex == stage.encounterIndex && state.players[1].deckId == encounter.deckId && state.players[1].leaderId == encounter.leaderId;
        }
        public static ScenarioStage StageForBattle(BattleState state)
        { return Array.Find(Stages, stage => IsScenarioBattle(stage, state)); }
        public static BattleEngine CreateBattle(string id, uint seed, string playerDeckId, string leaderId,
            IList<string> customDeck = null, RuleSet rules = null)
        {
            var stage = Get(id);
            return DemoCatalog.CreateBattle(seed, playerDeckId, leaderId, stage.encounterIndex, customDeck, rules);
        }
    }
}
