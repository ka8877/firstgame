using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    [Serializable] public sealed class LeaderDefinition
    {
        public string id, name, title, description, skillName, skillText;
        public Element skillElement;
        public int skillCost, artIndex;
        public LeaderSkill skill;
        public LeaderDefinition(string id, string name, string title, string description, string skillName,
            string skillText, Element element, LeaderSkill skill, int artIndex)
        {
            this.id = id; this.name = name; this.title = title; this.description = description;
            this.skillName = skillName; this.skillText = skillText; skillElement = element;
            skillCost = 2; this.skill = skill; this.artIndex = artIndex;
        }
    }

    [Serializable] public sealed class StarterDeckDefinition
    {
        public string id, name, description, recommendedLeaderId;
        public string[] cards;
        public StarterDeckDefinition(string id, string name, string description, string leader, params string[] cards)
        {
            this.id = id; this.name = name; this.description = description;
            recommendedLeaderId = leader; this.cards = cards;
        }
        public List<string> CopyCards() { return new List<string>(cards); }
    }

    [Serializable] public sealed class EncounterDefinition
    {
        public string id, name, description, deckId, leaderId;
        public int tier;
        public EncounterDefinition(string id, string name, string description, string deckId, string leaderId, int tier)
        {
            this.id = id; this.name = name; this.description = description;
            this.deckId = deckId; this.leaderId = leaderId; this.tier = tier;
        }
    }

    public static class DemoCatalog
    {
        public static readonly LeaderDefinition[] Leaders =
        {
            new LeaderDefinition("lyria", "리리아", "숲의 궁수", "작은 동료와 빠른 전개로 기회를 만드는 숲의 길잡이.", "꽃바람 화살",
                "여명 2 · 내 턴 1회. 남은 생명이 가장 낮은 적 유닛에게 피해 2(방어 적용). 적 유닛이 없으면 적 리더에게 피해 1.", Element.Dawn, LeaderSkill.QuickShot, 0),
            new LeaderDefinition("kael", "카엘", "태양 기사", "방패와 장비로 전열을 지키고 동료를 강화하는 기사.", "태양의 서약",
                "여명 2 · 내 턴 1회. 모든 아군 유닛의 공격 +1. 전투 동안 지속. 아군 유닛이 있어야 사용 가능.", Element.Dawn, LeaderSkill.Rally, 1),
            new LeaderDefinition("serin", "세린", "물결 마녀", "회복과 제거 마법으로 시간을 벌어 해룡을 부르는 마녀.", "호수의 숨결",
                "조류 2 · 내 턴 1회. 내 리더 생명 3 회복, 모든 아군 유닛의 피해 1 회복. 회복할 피해가 있어야 사용 가능.", Element.Tide, LeaderSkill.Restoration, 2),
            new LeaderDefinition("noa", "노아", "별의 기록자", "카드와 두 속성 자원을 연결하고 코인 확률을 활용하는 기록자.", "별의 인도",
                "조류 2 · 내 턴 1회. 카드 1장을 뽑고 여명 1 획득. 덱과 손패 여유가 있어야 사용 가능.", Element.Tide, LeaderSkill.Insight, 3)
        };

        public static readonly StarterDeckDefinition[] StarterDecks =
        {
            new StarterDeckDefinition("forest", "꽃바람의 숲", "빠른 소환과 공격 강화 · 낮은 비용의 궁수·요정으로 먼저 전장을 채웁니다.", "lyria",
                "dawn_grove", "dawn_grove", "dawn_grove", "dawn_grove", "dawn_court", "dawn_court", "tide_pool", "tide_pool",
                "dawn_scout", "dawn_scout", "dawn_sprite", "dawn_sprite", "dawn_archer", "dawn_archer", "dawn_archer", "dawn_falcon", "dawn_falcon", "tide_wisp",
                "dawn_chorus", "dawn_radiance", "dawn_bloom", "tide_insight", "dawn_longbow", "dawn_longbow"),
            new StarterDeckDefinition("knights", "백화 기사단", "방어와 장비 · 튼튼한 기사에게 장비와 리더 강화를 모아 오래 싸웁니다.", "kael",
                "dawn_sanctum", "dawn_sanctum", "dawn_sanctum", "dawn_spire", "dawn_spire", "dawn_spire", "tide_shore", "tide_shore",
                "dawn_scout", "dawn_scout", "dawn_guard", "dawn_guard", "dawn_paladin", "dawn_paladin", "dawn_lancer", "dawn_lancer", "dawn_captain", "dawn_captain",
                "dawn_mend", "dawn_oath", "dawn_chorus", "dawn_blade", "dawn_aegis", "dawn_aegis"),
            new StarterDeckDefinition("tides", "달호수의 노래", "제거와 회복 · 피해 마법으로 적을 정리하고 후반의 별바다 용을 지킵니다.", "serin",
                "tide_shore", "tide_shore", "tide_shore", "tide_pool", "tide_pool", "tide_pool", "dawn_court", "dawn_court",
                "tide_apprentice", "tide_apprentice", "tide_wisp", "tide_wisp", "tide_oracle", "tide_oracle", "tide_siren", "tide_siren", "tide_dragon", "tide_dragon",
                "tide_bolt", "tide_bolt", "tide_frost", "tide_tempest", "tide_mail", "tide_mail"),
            new StarterDeckDefinition("stars", "별술사의 서고", "뽑기와 코인 · 추가 손패와 코인 장비로 변동성 높은 전투를 준비합니다.", "noa",
                "tide_observatory", "tide_observatory", "tide_observatory", "tide_archive", "tide_archive", "tide_archive", "dawn_court", "dawn_court",
                "tide_apprentice", "tide_apprentice", "tide_keeper", "tide_oracle", "tide_astrologer", "tide_astrologer", "dawn_scout", "dawn_scout", "tide_dragon",
                "tide_insight", "tide_insight", "tide_frost", "dawn_radiance", "tide_charm", "tide_grimoire", "tide_crown")
        };

        public static readonly EncounterDefinition[] Encounters =
        {
            new EncounterDefinition("garden", "1장 · 꽃바람의 초대", "리리아와 첫 연습전. 지형 전개, 소환 대기, 공격과 방어를 익혀 보세요.", "forest", "lyria", 1),
            new EncounterDefinition("lake", "2장 · 달호수의 시험", "세린의 회복과 제거 마법을 넘어야 합니다. 장비와 리더 기술로 전열을 유지하세요.", "tides", "serin", 2),
            new EncounterDefinition("observatory", "3장 · 별빛의 서약", "노아의 추가 손패와 코인 장비에 도전합니다. 유닛을 지킬 때와 직접 공격할 때를 구분하세요.", "stars", "noa", 3)
        };

        public static LeaderDefinition GetLeader(string id)
        {
            return Array.Find(Leaders, x => x.id == id) ?? throw new ArgumentException("알 수 없는 리더: " + id);
        }
        public static StarterDeckDefinition GetDeck(string id)
        {
            return Array.Find(StarterDecks, x => x.id == id) ?? throw new ArgumentException("알 수 없는 기본 덱: " + id);
        }
        public static EncounterDefinition GetEncounter(int index)
        {
            if (index < 0 || index >= Encounters.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return Encounters[index];
        }
        public static BattleEngine CreateBattle(uint seed, string deckId, string leaderId, int encounterIndex = 0,
            IList<string> customDeck = null, RuleSet rules = null)
        {
            var starter = GetDeck(deckId); GetLeader(leaderId); var encounter = GetEncounter(encounterIndex);
            rules = rules ?? new RuleSet();
            var chosenDeck = customDeck == null ? starter.CopyCards() : new List<string>(customDeck);
            ValidatePlayableDeck(chosenDeck, rules.deckSize);
            return new BattleEngine(seed, rules, chosenDeck, GetDeck(encounter.deckId).CopyCards(),
                leaderId, encounter.leaderId, customDeck == null ? deckId : "custom", encounter.deckId, encounterIndex);
        }
        public static void ValidatePlayableDeck(IList<string> cards, int deckSize = 24)
        {
            if (cards == null || cards.Count != deckSize) throw new ArgumentException("덱은 " + deckSize + "장이어야 합니다.");
            var definitions = cards.Select(CardCatalog.Get).ToArray();
            if (cards.GroupBy(x => x).Any(g => g.Count() > 4)) throw new ArgumentException("같은 카드는 4장까지 넣을 수 있습니다.");
            if (definitions.Count(x => x.kind == CardKind.Terrain) < 4 || definitions.Count(x => x.kind == CardKind.Unit) < 6)
                throw new ArgumentException("지형 4장 이상, 유닛 6장 이상이 필요합니다.");
            foreach (Element element in Enum.GetValues(typeof(Element)))
                if (definitions.Any(x => x.cost > 0 && x.element == element) && !definitions.Any(x => x.kind == CardKind.Terrain && x.element == element))
                    throw new ArgumentException(CardCatalog.ElementName(element) + " 비용을 낼 지형이 필요합니다.");
        }
    }
}
