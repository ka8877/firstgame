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
            new LeaderDefinition("lyria", "리리아", "숲의 길잡이", "숲과 빛의 전열을 이끄는 아바타.", "클래식 아바타", "클래식 규칙에는 리더 기술이 없습니다.", Element.Forest, LeaderSkill.QuickShot, 0),
            new LeaderDefinition("kael", "카엘", "강철 지휘관", "불과 금속의 공격적인 전열을 이끄는 아바타.", "클래식 아바타", "클래식 규칙에는 리더 기술이 없습니다.", Element.Fire, LeaderSkill.Rally, 1),
            new LeaderDefinition("serin", "세린", "물결 마녀", "물과 암흑의 코인·제거 마법을 다루는 아바타.", "클래식 아바타", "클래식 규칙에는 리더 기술이 없습니다.", Element.Tide, LeaderSkill.Restoration, 2),
            new LeaderDefinition("noa", "노아", "대지의 기록자", "대지와 빛의 수비·강화로 전열을 지키는 아바타.", "클래식 아바타", "클래식 규칙에는 리더 기술이 없습니다.", Element.Earth, LeaderSkill.Insight, 3)
        };
        public static readonly StarterDeckDefinition[] StarterDecks =
        {
            Pair("forest", "숲과 빛 · 생명의 전열", "숲의 야수와 빛의 수호자. 겨울잠·홀리레인으로 전열을 지킵니다.", "lyria", Element.Forest, Element.Dawn),
            Pair("knights", "불과 금속 · 강철의 진격", "파이어볼·메테오와 금속 장비. 공격 준비에 소울을 집중합니다.", "kael", Element.Fire, Element.Metal),
            Pair("tides", "물과 암흑 · 심연의 파도", "물의 전열과 암흑 제거 마법. 코인과 공방 전용 마법을 활용합니다.", "serin", Element.Tide, Element.Darkness),
            Pair("stars", "대지와 빛 · 황금의 성채", "물리 피해·수비 마법과 빛의 강화. 다섯 칸의 배치 순서가 중요합니다.", "noa", Element.Earth, Element.Dawn)
        };
        static StarterDeckDefinition Pair(string id, string name, string text, string leader, Element first, Element second)
        {
            var pool = CardCatalog.All.Where(c => c.element == first || c.element == second).ToArray();
            var cards = new List<string>();
            foreach (var terrain in pool.Where(c => c.kind == CardKind.Terrain)) for (int i = 0; i < 4; i++) cards.Add(terrain.id);
            var units = pool.Where(c => c.kind == CardKind.Unit).OrderBy(c => c.level).ThenBy(c => c.id).ToArray();
            cards.AddRange(units.Select(c => c.id)); cards.AddRange(units.Take(8).Select(c => c.id));
            var spells = pool.Where(c => c.kind == CardKind.Spell).OrderBy(c => c.level).ThenBy(c => c.id).ToArray();
            cards.AddRange(spells.Select(c => c.id)); cards.AddRange(spells.Take(2).Select(c => c.id));
            var items = pool.Where(c => c.kind == CardKind.Equipment).OrderBy(c => c.level).ThenBy(c => c.id).ToArray();
            if (items.Length == 0) throw new InvalidOperationException("기본 덱의 아이템이 없습니다: " + id);
            for (int i = 0; i < 4; i++) cards.Add(items[i % items.Length].id);
            if (cards.Count != 50) throw new InvalidOperationException("기본 덱 구성은 50장이어야 합니다: " + id);
            return new StarterDeckDefinition(id, name, text, leader, cards.ToArray());
        }
        public static readonly EncounterDefinition[] Encounters =
        {
            new EncounterDefinition("garden", "1장 · 숲의 첫 결투", "LP와 지형, 소울, 양측 준비 완료와 전열 순서 전투를 익힙니다.", "forest", "lyria", 1),
            new EncounterDefinition("lake", "2장 · 심연의 시험", "물·암흑의 코인과 공방 전용 마법에 대응하세요.", "tides", "serin", 2),
            new EncounterDefinition("observatory", "3장 · 강철의 관문", "불·금속의 제거와 장비 전열을 넘고 상대 덱을 소진시키세요.", "knights", "kael", 3)
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
        public static void ValidatePlayableDeck(IList<string> cards, int deckSize = 50)
        {
            if (cards == null || cards.Count != deckSize) throw new ArgumentException("덱은 " + deckSize + "장이어야 합니다.");
            var definitions = cards.Select(CardCatalog.Get).ToArray();
            if (cards.GroupBy(x => x).Any(g => g.Count() > CardCatalog.Get(g.Key).deckLimit)) throw new ArgumentException("카드에 표시된 동일 카드 한도를 지켜야 합니다.");
            if (!definitions.Any(x => x.kind == CardKind.Terrain)) throw new ArgumentException("지형이 최소 1장 필요합니다.");
            foreach (Element element in Enum.GetValues(typeof(Element)))
                if (definitions.Any(x => x.cost > 0 && x.element == element) && !definitions.Any(x => x.kind == CardKind.Terrain && x.element == element))
                    throw new ArgumentException(CardCatalog.ElementName(element) + " 비용을 낼 지형이 필요합니다.");
        }
    }
}
