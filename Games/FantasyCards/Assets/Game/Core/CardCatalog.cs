using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public static class CardCatalog
    {
        public const string Version = "starlight-0.1.0";
        public static readonly CardDefinition[] All =
        {
            new CardDefinition("dawn_court", "새벽의 정원", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("dawn_spire", "별빛 첨탑", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("tide_shore", "달물결 해안", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("tide_pool", "기억의 샘", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("dawn_scout", "별길의 정찰자", "코인 앞면마다 공격 +1.", CardKind.Unit, Element.Dawn, 1, 2, 3, 1),
            new CardDefinition("dawn_guard", "서약의 수호자", "코인 앞면마다 공격 +1. 오래 버티는 수호자.", CardKind.Unit, Element.Dawn, 2, 2, 5, 1),
            new CardDefinition("dawn_lancer", "태양창 기사", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Dawn, 3, 4, 4, 2),
            new CardDefinition("tide_wisp", "물안개 요정", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 1, 1, 3, 2),
            new CardDefinition("tide_keeper", "샘의 기록자", "코인 앞면마다 공격 +1.", CardKind.Unit, Element.Tide, 2, 3, 4, 1),
            new CardDefinition("tide_serpent", "은비늘 해룡", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 3, 3, 6, 2),
            new CardDefinition("dawn_mend", "여명의 기도", "선택한 아군 유닛의 피해 3 회복.", CardKind.Spell, Element.Dawn, 1, effect: Effect.Heal, magnitude: 3),
            new CardDefinition("tide_bolt", "파도의 화살", "선택한 적 유닛에게 피해 2. 방어구 적용.", CardKind.Spell, Element.Tide, 1, effect: Effect.Damage, magnitude: 2),
            new CardDefinition("dawn_oath", "함께한 서약", "선택한 아군 유닛의 공격 +1. 전투 동안 지속.", CardKind.Spell, Element.Dawn, 1, effect: Effect.Rally, magnitude: 1),
            new CardDefinition("dawn_blade", "유성검", "장착 유닛의 공격 +1.", CardKind.Equipment, Element.Dawn, 1, effect: Effect.Power, magnitude: 1),
            new CardDefinition("tide_mail", "물결 갑옷", "장착 유닛이 받는 피해마다 1 감소.", CardKind.Equipment, Element.Tide, 1, effect: Effect.Armor, magnitude: 1),
            new CardDefinition("tide_charm", "달의 표식", "장착 유닛의 전투 코인 +1.", CardKind.Equipment, Element.Tide, 1, effect: Effect.Coin, magnitude: 1)
        };
        public static CardDefinition Get(string id)
        {
            var card = Array.Find(All, x => x.id == id);
            if (card == null) throw new ArgumentException("알 수 없는 카드 ID: " + id);
            return card;
        }
        public static void ApplyAuthoredDefinitions(IEnumerable<CardDefinition> definitions)
        {
            var authored = definitions.ToArray();
            if (authored.Length != All.Length || authored.Select(d => d.id).Distinct().Count() != All.Length || authored.Any(d => !All.Any(c => c.id == d.id))) throw new ArgumentException("제작 데이터의 안정적인 카드 ID 16종이 필요합니다.");
            foreach (var d in authored)
                if (string.IsNullOrWhiteSpace(d.name) || d.cost < 0 || d.cost > 100 || d.attack < 0 || d.health < 0 || d.coinBonus < 0 || d.coinBonus > 20 || d.magnitude < 0 || d.magnitude > 20 || !Enum.IsDefined(typeof(CardKind), d.kind) || !Enum.IsDefined(typeof(Element), d.element) || !Enum.IsDefined(typeof(Effect), d.effect) || (d.kind == CardKind.Unit && d.health < 1)) throw new ArgumentException("잘못된 카드 제작 데이터: " + d.id);
            for (int i = 0; i < All.Length; i++) All[i] = authored.Single(d => d.id == All[i].id);
        }
        public static List<string> DefaultDeck(bool opponent = false)
        {
            var cards = new List<string>();
            foreach (var card in All)
            {
                int count = card.kind == CardKind.Terrain ? 2 : 1;
                for (int i = 0; i < count; i++) cards.Add(card.id);
            }
            cards.AddRange(opponent
                ? new[] { "tide_wisp", "tide_keeper", "dawn_scout", "tide_serpent" }
                : new[] { "dawn_scout", "dawn_guard", "tide_wisp", "dawn_lancer" });
            return cards;
        }
        public static string KindName(CardKind kind) { return new[] { "지형", "유닛", "마법", "장비" }[(int)kind]; }
        public static string ElementName(Element element) { return element == Element.Dawn ? "여명" : "조류"; }
    }
}
